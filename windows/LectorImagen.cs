// Reconoce eventos en una imagen de un calendario (captura de pantalla o archivo) con el OCR que trae Windows
// (Windows.Media.Ocr: sin conexión y sin instalar nada). Entiende la vista "Programación" de Google Calendar
// (filas con fecha, hora y título) y las vistas de día y semana (columnas con el día arriba).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

static class LectorImagen
{
    // Un trozo de texto reconocido y dónde está en la imagen.
    public class Linea
    {
        public string Texto = "", Simple = "";
        public RectangleF Caja;
        public bool Usada;
        public float Centro { get { return Caja.X + Caja.Width / 2; } }
    }

    // ---------- OCR ----------

    // Texto de la imagen en trozos con su posición (lanza una excepción si Windows no tiene OCR en español).
    public static List<Linea> LeerTexto(Bitmap imagen)
    {
        // El OCR acierta más con letra de tamaño medio: se amplía si la imagen es pequeña y se reduce si es enorme.
        float escala = 1f;
        if (imagen.Height < 500) escala = 2f;
        int maximo = (int)Windows.Media.Ocr.OcrEngine.MaxImageDimension;
        if (Math.Max(imagen.Width, imagen.Height) * escala > maximo) escala = maximo / (float)Math.Max(imagen.Width, imagen.Height);

        int w = Math.Max(1, (int)(imagen.Width * escala)), h = Math.Max(1, (int)(imagen.Height * escala));
        byte[] px;
        using (Bitmap b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(b))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(imagen, 0, 0, w, h);
            }
            BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            px = new byte[d.Stride * h];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            b.UnlockBits(d);
        }

        Windows.Media.Ocr.OcrEngine motor = Windows.Media.Ocr.OcrEngine.TryCreateFromUserProfileLanguages();
        if (motor == null)
            try { motor = Windows.Media.Ocr.OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("es")); } catch { }
        if (motor == null)
            throw new Exception("Windows no tiene el reconocimiento de texto en español. Instálalo en Configuración → Hora e idioma → " +
                                "Idioma y región → Español → Opciones de idioma → Reconocimiento óptico de caracteres.");

        Windows.Graphics.Imaging.SoftwareBitmap sb = Windows.Graphics.Imaging.SoftwareBitmap.CreateCopyFromBuffer(
            Windows.Security.Cryptography.CryptographicBuffer.CreateFromByteArray(px), Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, w, h);
        Windows.Foundation.IAsyncOperation<Windows.Media.Ocr.OcrResult> op = motor.RecognizeAsync(sb);
        while (op.Status == Windows.Foundation.AsyncStatus.Started) Thread.Sleep(20);
        if (op.Status != Windows.Foundation.AsyncStatus.Completed) throw new Exception("no se pudo leer la imagen.");

        List<Linea> lineas = new List<Linea>();
        foreach (Windows.Media.Ocr.OcrLine l in op.GetResults().Lines)
        {
            if (l.Words.Count == 0) continue;
            RectangleF caja = RectangleF.Empty;
            foreach (Windows.Media.Ocr.OcrWord p in l.Words)
            {
                RectangleF r = new RectangleF((float)p.BoundingRect.X / escala, (float)p.BoundingRect.Y / escala,
                                              (float)p.BoundingRect.Width / escala, (float)p.BoundingRect.Height / escala);
                caja = caja.IsEmpty ? r : RectangleF.Union(caja, r);
            }
            Linea li = new Linea();
            li.Texto = l.Text;
            li.Caja = caja;
            lineas.Add(li);
        }
        return lineas;
    }

    // ---------- Patrones ----------

    static readonly string[] MESES = { "ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic" };
    static readonly string[] DIAS = { "dom", "lun", "mar", "mie", "jue", "vie", "sab" }; // como DayOfWeek

    // "mar 3 nov", "martes, 3 de noviembre", "3 nov, mar", "hoy", "mañana"
    // (el número no puede ser una hora: "mar 9:30" es martes + hora, no "martes 9")
    static readonly Regex DIA_NUMERO = new Regex(@"\b(lun|mar|mie|jue|vie|sab|dom)[a-z]*\.?,?\s+(\d{1,2})\b(?![:.]\d|\s?[ap]\.?\s?m)(?:\s*(?:de\s+)?(ene|feb|mar|abr|may|jun|jul|ago|sep|set|oct|nov|dic)[a-z]*\.?)?");
    static readonly Regex NUMERO_MES = new Regex(@"\b(\d{1,2})\s*(?:de\s+)?(ene|feb|mar|abr|may|jun|jul|ago|sep|set|oct|nov|dic)[a-z]*\.?(?:,?\s*(?:lun|mar|mie|jue|vie|sab|dom)[a-z]*\.?)?");
    static readonly Regex HOY_MANANA = new Regex(@"^\s*(hoy|manana)\b");
    // Cabecera de columna en la vista semana: "LUN", "mar 3", "MIÉ."
    static readonly Regex CABECERA = new Regex(@"^(lun|mar|mie|jue|vie|sab|dom)[a-z]*\.?,?(?:\s+(\d{1,2}))?$");

    // 9:30, 9.30, 9:30am, 9:30 p. m., 21:00, 9am. Para que cuente como hora debe tener minutos o am/pm.
    const string HORA = @"(\d{1,2})(?:[:.](\d{2}))?\s*(a\.?\s?m\.?|p\.?\s?m\.?)?";
    static readonly Regex RANGO = new Regex(@"(?<![\d:])" + HORA + @"\s*(?:-|a\b|hasta|to)?\s*" + HORA + @"(?![\d])");
    static readonly Regex SOLA = new Regex(@"(?<![\d:])" + HORA + @"(?![\d])");
    static readonly Regex TODO_EL_DIA = new Regex(@"todo el dia");

    // Minúsculas, sin tildes y con los errores típicos del OCR en las horas corregidos.
    // Conserva la longitud del texto, para poder quitar trozos del original por posición.
    static string Simplificar(string s)
    {
        string n = s.ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (char c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(c == '–' || c == '—' ? '-' : c);
        }
        string r = sb.ToString();
        if (r.Length != s.Length) r = s.ToLowerInvariant(); // por si el texto tenía caracteres raros
        // Letras que el OCR confunde con cifras en las horas: "g — loam" → "9 - 10am", "llam" → "11am", "1O:3O" → "10:30".
        // Solo dentro de lo que claramente es una hora (rango con am/pm, o seguido de am/pm o de :mm).
        const string CIFRA = @"[0-9lio|gs]";
        r = Regex.Replace(r, @"(?<![a-z])(" + CIFRA + @"{1,2}(?:[:.]" + CIFRA + @"{2})?)(\s*-\s*)(" + CIFRA + @"{1,2}(?:[:.]" + CIFRA + @"{2})?)(?=\s*(?:am|pm|a\.\s?m|p\.\s?m))",
                          m => Cifras(m.Groups[1].Value) + m.Groups[2].Value + Cifras(m.Groups[3].Value));
        r = Regex.Replace(r, @"(?<![a-z])(" + CIFRA + @"{1,2})(?=\s?(?:am|pm|a\.\s?m|p\.\s?m)\b|[:.]\d)", m => Cifras(m.Value));
        r = Regex.Replace(r, @"(?<=\d)o|o(?=\d)", "0");
        return r;
    }

    // Cambia letras parecidas por cifras (misma longitud).
    static string Cifras(string s)
    {
        char[] c = s.ToCharArray();
        for (int i = 0; i < c.Length; i++)
            switch (c[i])
            {
                case 'l': case 'i': case '|': c[i] = '1'; break;
                case 'o': c[i] = '0'; break;
                case 'g': c[i] = '9'; break;
                case 's': c[i] = '5'; break;
            }
        return new string(c);
    }

    // ---------- De texto a eventos ----------

    // Convierte el texto de la imagen en eventos. "hoy" sirve para completar mes y año cuando no se ven.
    public static List<EventoCalendario> Reconocer(List<Linea> piezas, DateTime hoy, string fuente)
    {
        foreach (Linea p in piezas) p.Simple = Simplificar(p.Texto);
        float espacio;
        List<KeyValuePair<Linea, DateTime>> columnas = Columnas(piezas, hoy, out espacio);
        List<EventoCalendario> res = columnas.Count >= 2 ? PorColumnas(piezas, columnas, espacio, fuente) : PorFilas(piezas, hoy, fuente);

        List<EventoCalendario> unicos = new List<EventoCalendario>();
        foreach (EventoCalendario e in res)
            if (!unicos.Exists(delegate(EventoCalendario o) { return o.Uid == e.Uid; })) unicos.Add(e);
        unicos.Sort(delegate(EventoCalendario a, EventoCalendario b) { return a.Inicio.CompareTo(b.Inicio); });
        return unicos;
    }

    // Vista "Programación" (y listas en general): se juntan los trozos de cada fila; la fecha vale hasta la siguiente.
    static List<EventoCalendario> PorFilas(List<Linea> piezas, DateTime hoy, string fuente)
    {
        List<Linea> filas = Filas(piezas);
        List<EventoCalendario> res = new List<EventoCalendario>();
        DateTime dia = hoy.Date;
        for (int i = 0; i < filas.Count; i++)
        {
            Linea f = filas[i];
            DateTime fecha;
            Match quitar;
            if (BuscarFecha(f.Simple, hoy, out fecha, out quitar)) dia = fecha;

            EventoCalendario e = Evento(f, dia, quitar, fuente);
            if (e == null) continue;
            if (e.Titulo.Length < 2)
            {
                // Sin título en la misma fila: el de la fila siguiente (o la anterior) si no tiene hora ni fecha.
                foreach (int k in new int[] { i + 1, i - 1 })
                    if (k >= 0 && k < filas.Count && !filas[k].Usada && EsTitulo(filas[k], hoy))
                    {
                        e.Titulo = Limpiar(filas[k].Texto, filas[k].Simple, null, null);
                        filas[k].Usada = true;
                        break;
                    }
            }
            Terminar(e);
            res.Add(e);
        }
        return res;
    }

    // Marcas del eje de horas: "7 AM", "12 PM", "13:00" (con am/pm o minutos, para no confundirlas con días).
    static readonly Regex HORA_EJE = new Regex(@"^\d{1,2}(?:(?:[:.]\d{2})\s?(?:a\.?\s?m\.?|p\.?\s?m\.?)?|\s?(?:a\.?\s?m\.?|p\.?\s?m\.?))$");

    // Vista semana o día: cada trozo pertenece al día de su columna; el título suele ir encima de la hora.
    // Se ignora lo que está a la izquierda de la primera columna (barra lateral, horas del eje).
    // Lo que está entre la cabecera y la primera hora del eje son eventos de todo el día.
    static List<EventoCalendario> PorColumnas(List<Linea> piezas, List<KeyValuePair<Linea, DateTime>> columnas, float espacio, string fuente)
    {
        float abajoCabecera = 0;
        foreach (KeyValuePair<Linea, DateTime> c in columnas) abajoCabecera = Math.Max(abajoCabecera, c.Key.Caja.Bottom);
        // Los números de los días van debajo de los nombres: también son cabecera.
        foreach (Linea p in piezas)
        {
            int n;
            if (int.TryParse(p.Simple.Trim(), out n) && p.Caja.Y >= abajoCabecera - 2 && p.Caja.Y - abajoCabecera < columnas[0].Key.Caja.Height * 3)
                foreach (KeyValuePair<Linea, DateTime> c in columnas)
                    if (Math.Abs(c.Key.Centro - p.Centro) < c.Key.Caja.Width * 1.5f) abajoCabecera = Math.Max(abajoCabecera, p.Caja.Bottom);
        }
        // Eje de horas ("7 AM", "8 AM"…), a la izquierda de las columnas: marca el borde izquierdo del calendario
        // (lo que queda más a la izquierda, como la barra lateral, se ignora) y el final de "todo el día".
        float arribaCabecera = float.MaxValue;
        foreach (KeyValuePair<Linea, DateTime> c in columnas) arribaCabecera = Math.Min(arribaCabecera, c.Key.Caja.Y);
        float alto = Math.Max(columnas[0].Key.Caja.Height * 2, (abajoCabecera - arribaCabecera) / 3); // ~ una línea de evento
        // Las marcas del eje están alineadas a la derecha, una debajo de otra (al menos dos con el mismo borde derecho);
        // así no se confunden con la hora de un evento.
        float izquierda = float.MinValue, primeraHora = float.MaxValue;
        List<Linea> horas = piezas.FindAll(delegate(Linea p)
        {
            return p.Centro < columnas[0].Key.Centro && p.Caja.Y > abajoCabecera && HORA_EJE.IsMatch(p.Simple.Trim());
        });
        foreach (Linea p in horas)
        {
            int alineadas = horas.FindAll(delegate(Linea o) { return Math.Abs(o.Caja.Right - p.Caja.Right) < 5; }).Count;
            if (alineadas < 2) continue;
            izquierda = Math.Max(izquierda, p.Caja.Right);
            primeraHora = Math.Min(primeraHora, p.Caja.Y + p.Caja.Height * 0.5f);
        }
        if (izquierda == float.MinValue) // sin eje: se admite una columna más a la izquierda (por si el OCR no leyó su nombre)
            izquierda = columnas[0].Key.Centro - (espacio > 0 ? espacio * 1.5f : columnas[0].Key.Caja.Width * 2);
        // Los eventos de todo el día van justo debajo de la cabecera (unas pocas líneas de alto).
        float finTodoElDia = Math.Min(primeraHora, abajoCabecera + alto * 6);

        Dictionary<Linea, DateTime> diaDe = new Dictionary<Linea, DateTime>();
        List<Linea> cuerpo = new List<Linea>();
        foreach (Linea p in piezas)
        {
            if (p.Caja.Y < abajoCabecera || p.Centro < izquierda) continue;
            // Columna más cercana; si está más lejos (columna que el OCR no leyó), se cuenta cuántas columnas hay de distancia.
            KeyValuePair<Linea, DateTime> cerca = columnas[0];
            foreach (KeyValuePair<Linea, DateTime> c in columnas)
                if (Math.Abs(c.Key.Centro - p.Centro) < Math.Abs(cerca.Key.Centro - p.Centro)) cerca = c;
            int salto = espacio > 0 ? (int)Math.Round((p.Centro - cerca.Key.Centro) / espacio) : 0;
            diaDe[p] = cerca.Value.AddDays(salto);
            cuerpo.Add(p);
        }
        cuerpo.Sort(delegate(Linea a, Linea b) { return a.Caja.Y.CompareTo(b.Caja.Y); });

        List<EventoCalendario> res = new List<EventoCalendario>();
        foreach (Linea p in cuerpo)
        {
            if (p.Usada) continue;
            EventoCalendario e = Evento(p, diaDe[p], null, fuente);
            if (e == null) continue;
            if (e.Titulo.Length < 2)
            {
                // El título va encima de la hora y puede ocupar varias líneas: se juntan las que están pegadas.
                List<Linea> titulo = new List<Linea>();
                Linea abajo = p;
                while (true)
                {
                    Linea mejor = null;
                    float distancia = float.MaxValue;
                    foreach (Linea o in cuerpo)
                    {
                        if (o == p || o.Usada || titulo.Contains(o) || diaDe[o] != diaDe[p] || !EsTitulo(o, DateTime.Today)) continue;
                        float dy = abajo.Caja.Y - o.Caja.Bottom;
                        if (o.Caja.Y >= abajo.Caja.Y || dy < -4 || dy > abajo.Caja.Height * (titulo.Count == 0 ? 2.2f : 0.9f)) continue;
                        if (dy < distancia) { distancia = dy; mejor = o; }
                    }
                    if (mejor == null || titulo.Count >= 3) break;
                    titulo.Insert(0, mejor);
                    abajo = mejor;
                }
                if (titulo.Count == 0) // o, si no hay nada encima, justo debajo
                    foreach (Linea o in cuerpo)
                        if (o != p && !o.Usada && diaDe[o] == diaDe[p] && EsTitulo(o, DateTime.Today) &&
                            o.Caja.Y > p.Caja.Y && o.Caja.Y - p.Caja.Bottom < p.Caja.Height * 1.2f) { titulo.Add(o); break; }
                List<string> partes = new List<string>();
                foreach (Linea t in titulo)
                {
                    partes.Add(Limpiar(t.Texto, t.Simple, null, null));
                    t.Usada = true;
                }
                e.Titulo = string.Join(" ", partes.ToArray());
            }
            Terminar(e);
            res.Add(e);
        }

        // Eventos de todo el día: textos justo debajo de la cabecera (antes de la primera hora del eje).
        foreach (Linea p in cuerpo)
            {
                if (p.Usada || p.Caja.Bottom > finTodoElDia + 4 || !EsTitulo(p, DateTime.Today)) continue;
                EventoCalendario e = new EventoCalendario();
                e.Fuente = fuente;
                e.TodoElDia = true;
                e.Inicio = diaDe[p];
                e.Fin = diaDe[p].AddDays(1);
                e.Titulo = Limpiar(p.Texto, p.Simple, null, null);
                p.Usada = true;
                Terminar(e);
                res.Add(e);
            }
        return res;
    }

    // Cabeceras de columna (vista semana/día): al menos dos días de la semana a la misma altura.
    static List<KeyValuePair<Linea, DateTime>> Columnas(List<Linea> piezas, DateTime hoy, out float espacio)
    {
        espacio = 0;
        List<Linea> candidatas = piezas.FindAll(delegate(Linea p) { return CABECERA.IsMatch(p.Simple.Trim()); });
        List<Linea> fila = new List<Linea>();
        foreach (Linea c in candidatas)
        {
            List<Linea> f = candidatas.FindAll(delegate(Linea o) { return Math.Abs(o.Caja.Y - c.Caja.Y) < c.Caja.Height; });
            if (f.Count > fila.Count) fila = f;
        }
        List<KeyValuePair<Linea, DateTime>> res = new List<KeyValuePair<Linea, DateTime>>();
        if (fila.Count < 2) return res;
        fila.Sort(delegate(Linea a, Linea b) { return a.Centro.CompareTo(b.Centro); });

        // Número del día: junto al nombre o en un trozo con solo el número justo debajo.
        int[] dow = new int[fila.Count], num = new int[fila.Count];
        for (int i = 0; i < fila.Count; i++)
        {
            Match m = CABECERA.Match(fila[i].Simple.Trim());
            dow[i] = Array.IndexOf(DIAS, m.Groups[1].Value);
            num[i] = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (num[i] == 0)
                foreach (Linea p in piezas)
                {
                    int n;
                    if (p.Caja.Y > fila[i].Caja.Y && p.Caja.Y - fila[i].Caja.Bottom < fila[i].Caja.Height * 3 &&
                        Math.Abs(p.Centro - fila[i].Centro) < fila[i].Caja.Width * 1.5f && int.TryParse(p.Simple.Trim(), out n) && n >= 1 && n <= 31)
                    { num[i] = n; break; }
                }
        }

        // Fechas: con el número se busca la fecha más cercana a hoy de ese día de la semana; sin números, la semana actual.
        DateTime referencia = DateTime.MinValue;
        int refIndice = -1;
        for (int i = 0; i < fila.Count && refIndice < 0; i++)
            if (num[i] > 0)
            {
                DateTime f;
                if (FechaCercana(num[i], 0, dow[i], hoy, out f)) { referencia = f; refIndice = i; }
            }
        DateTime lunes = hoy.Date.AddDays(-(((int)hoy.DayOfWeek + 6) % 7));
        for (int i = 0; i < fila.Count; i++)
        {
            DateTime d;
            if (refIndice >= 0)
            {
                int dif = ((dow[i] - dow[refIndice]) % 7 + 7) % 7;
                if (i < refIndice) dif -= 7;
                d = referencia.AddDays(dif);
            }
            else d = lunes.AddDays((dow[i] + 6) % 7);
            res.Add(new KeyValuePair<Linea, DateTime>(fila[i], d));
        }
        // Separación entre columnas (para las que el OCR no leyó)
        float suma = 0;
        int cuenta = 0;
        for (int i = 1; i < res.Count; i++)
        {
            int dias = (int)(res[i].Value - res[i - 1].Value).TotalDays;
            if (dias > 0) { suma += (res[i].Key.Centro - res[i - 1].Key.Centro) / dias; cuenta++; }
        }
        espacio = cuenta > 0 ? suma / cuenta : 0;
        return res;
    }

    // Junta los trozos que están a la misma altura (una fila de la vista Programación), de izquierda a derecha.
    static List<Linea> Filas(List<Linea> piezas)
    {
        List<Linea> orden = new List<Linea>(piezas);
        orden.Sort(delegate(Linea a, Linea b) { return a.Caja.Y.CompareTo(b.Caja.Y); });
        List<List<Linea>> grupos = new List<List<Linea>>();
        foreach (Linea p in orden)
        {
            List<Linea> grupo = null;
            foreach (List<Linea> g in grupos)
            {
                RectangleF r = g[0].Caja;
                float solape = Math.Min(r.Bottom, p.Caja.Bottom) - Math.Max(r.Y, p.Caja.Y);
                if (solape > Math.Min(r.Height, p.Caja.Height) * 0.5f) { grupo = g; break; }
            }
            if (grupo == null) { grupo = new List<Linea>(); grupos.Add(grupo); }
            grupo.Add(p);
        }
        List<Linea> filas = new List<Linea>();
        foreach (List<Linea> g in grupos)
        {
            g.Sort(delegate(Linea a, Linea b) { return a.Caja.X.CompareTo(b.Caja.X); });
            Linea f = new Linea();
            f.Caja = g[0].Caja;
            foreach (Linea p in g)
            {
                f.Texto += (f.Texto == "" ? "" : "  ") + p.Texto;
                f.Simple += (f.Simple == "" ? "" : "  ") + p.Simple;
                f.Caja = RectangleF.Union(f.Caja, p.Caja);
            }
            filas.Add(f);
        }
        return filas;
    }

    // Evento a partir de un trozo con hora o "todo el día" (null si no tiene). El título es lo que queda del texto.
    static EventoCalendario Evento(Linea l, DateTime dia, Match fechaQuitar, string fuente)
    {
        string s = l.Simple;
        Match todo = TODO_EL_DIA.Match(s);
        Match m = RANGO.Match(s);
        bool rango = m.Success && ValidaRango(m);
        if (!rango) m = SOLA.Match(s);
        bool hora = rango || (m.Success && (m.Groups[2].Success || m.Groups[3].Success) && int.Parse(m.Groups[1].Value) <= 23);
        if (!hora && !todo.Success) return null;

        EventoCalendario e = new EventoCalendario();
        e.Fuente = fuente;
        if (hora)
        {
            int h1 = int.Parse(m.Groups[1].Value), m1 = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            string ap1 = m.Groups[3].Value;
            if (rango)
            {
                int h2 = int.Parse(m.Groups[4].Value), m2 = m.Groups[5].Success ? int.Parse(m.Groups[5].Value) : 0;
                string ap2 = m.Groups[6].Value;
                if (ap1 == "" && ap2 != "" && Hora24(h1, ap2) <= Hora24(h2, ap2)) ap1 = ap2; // "1 – 2pm" = 1pm – 2pm
                e.Inicio = dia.AddHours(Hora24(h1, ap1)).AddMinutes(Math.Min(m1, 59));
                e.Fin = dia.AddHours(Hora24(h2, ap2 == "" ? ap1 : ap2)).AddMinutes(Math.Min(m2, 59));
                if (e.Fin <= e.Inicio) e.Fin = e.Fin.AddDays(1);
            }
            else
            {
                e.Inicio = dia.AddHours(Hora24(h1, ap1)).AddMinutes(Math.Min(m1, 59));
                e.Fin = e.Inicio.AddHours(1);
            }
        }
        else
        {
            e.TodoElDia = true;
            e.Inicio = dia;
            e.Fin = dia.AddDays(1);
        }
        e.Titulo = Limpiar(l.Texto, s, hora ? m : todo, fechaQuitar);
        return e;
    }

    static void Terminar(EventoCalendario e)
    {
        if (e.Titulo.Length < 2) e.Titulo = "Evento";
        e.Uid = "imagen-" + (e.Titulo.ToLowerInvariant() + e.Inicio.ToString("yyyyMMddHHmm")).GetHashCode().ToString("x8");
    }

    // ¿Es un trozo de solo texto (sin hora ni fecha), que puede ser el título de un evento?
    static bool EsTitulo(Linea l, DateTime hoy)
    {
        string s = l.Simple.Trim();
        if (s.Length < 2 || TODO_EL_DIA.IsMatch(s) || CABECERA.IsMatch(s)) return false;
        Match r = RANGO.Match(s);
        if (r.Success && ValidaRango(r)) return false;
        Match h = SOLA.Match(s);
        if (h.Success && (h.Groups[2].Success || h.Groups[3].Success)) return false;
        DateTime f;
        Match q;
        return !BuscarFecha(s, hoy, out f, out q);
    }

    static bool ValidaRango(Match m)
    {
        // Al menos una de las dos horas debe tener minutos o am/pm (si no, son números sueltos, no horas).
        bool primera = m.Groups[2].Success || m.Groups[3].Success, segunda = m.Groups[5].Success || m.Groups[6].Success;
        int h1 = int.Parse(m.Groups[1].Value), h2 = int.Parse(m.Groups[4].Value);
        return (primera || segunda) && h1 <= 23 && h2 <= 23;
    }

    static int Hora24(int h, string ampm)
    {
        string a = ampm.Replace(".", "").Replace(" ", "");
        if (a == "pm" && h < 12) return h + 12;
        if (a == "am" && h == 12) return 0;
        return Math.Min(h, 23);
    }

    // El título: el texto original sin la hora, la fecha ni "todo el día" (se quitan por posición).
    static string Limpiar(string original, string simple, Match hora, Match fecha)
    {
        char[] c = original.ToCharArray();
        foreach (Match m in new Match[] { hora, fecha })
            if (m != null && m.Success && simple.Length == original.Length)
                for (int i = m.Index; i < m.Index + m.Length && i < c.Length; i++) c[i] = ' ';
        string s = Regex.Replace(new string(c), @"\s+", " ");
        return s.Trim(' ', ',', '.', '·', '-', '–', '—', '|', ':', '•');
    }

    // Busca una fecha en el texto simplificado. "quitar" es el trozo que la forma.
    static bool BuscarFecha(string s, DateTime hoy, out DateTime fecha, out Match quitar)
    {
        fecha = hoy.Date;
        quitar = HOY_MANANA.Match(s);
        if (quitar.Success)
        {
            fecha = hoy.Date.AddDays(quitar.Groups[1].Value == "hoy" ? 0 : 1);
            return true;
        }
        // Primero "3 nov" (con mes, más fiable) y luego "mar 3".
        quitar = NUMERO_MES.Match(s);
        if (quitar.Success)
        {
            Match ds = Regex.Match(quitar.Value, @"(lun|mar|mie|jue|vie|sab|dom)[a-z]*\.?$");
            return FechaCercana(int.Parse(quitar.Groups[1].Value), Mes(quitar.Groups[2].Value),
                                ds.Success ? Array.IndexOf(DIAS, ds.Groups[1].Value) : -1, hoy, out fecha);
        }
        quitar = DIA_NUMERO.Match(s);
        if (quitar.Success)
            return FechaCercana(int.Parse(quitar.Groups[2].Value), quitar.Groups[3].Success ? Mes(quitar.Groups[3].Value) : 0,
                                Array.IndexOf(DIAS, quitar.Groups[1].Value), hoy, out fecha);
        return false;
    }

    // La fecha con ese día (y ese mes y día de la semana, si se conocen) más cercana a hoy; se prefieren las futuras.
    static bool FechaCercana(int dia, int mes, int diaSemana, DateTime hoy, out DateTime fecha)
    {
        fecha = hoy.Date;
        if (dia < 1 || dia > 31) return false;
        DateTime mejor = DateTime.MinValue;
        double distancia = double.MaxValue;
        for (int k = -2; k <= 13; k++)
        {
            DateTime b = new DateTime(hoy.Year, hoy.Month, 1).AddMonths(k);
            if (mes > 0 && b.Month != mes) continue;
            if (dia > DateTime.DaysInMonth(b.Year, b.Month)) continue;
            DateTime c = new DateTime(b.Year, b.Month, dia);
            if (diaSemana >= 0 && (int)c.DayOfWeek != diaSemana) continue;
            double dd = Math.Abs((c - hoy.Date).TotalDays) + (c < hoy.Date.AddDays(-7) ? 30 : 0);
            if (dd < distancia) { distancia = dd; mejor = c; }
        }
        if (mejor == DateTime.MinValue) return false;
        fecha = mejor;
        return true;
    }

    static int Mes(string m) { return m == "set" ? 9 : Array.IndexOf(MESES, m) + 1; }
}
