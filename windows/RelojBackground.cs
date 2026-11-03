// Overlay transparente para Windows.
// Muestra un reloj en vivo (6 diseños, varios colores) o una imagen GIF/PNG siempre encima,
// sin capturar el ratón ni el teclado: puedes seguir trabajando debajo.
// Se controla con atajos de teclado globales, la línea de comandos y una ventana de configuración.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    public const string NOMBRE = "RelojBackground";
    public const string TITULO = "RelojBackground_7f3a91"; // ventana oculta que recibe los comandos
    public const int WM_COMANDO = 0x8000 + 0x51; // WM_APP + n

    public const int CMD_ALTERNAR = 1, CMD_SALIR = 2, CMD_MOSTRAR = 3, CMD_OCULTAR = 4, CMD_CONFIG = 5;

    [STAThread]
    static void Main(string[] args)
    {
        int comando = CMD_ALTERNAR;
        foreach (string a in args)
        {
            string s = a.ToLowerInvariant();
            if (s == "--salir") comando = CMD_SALIR;
            else if (s == "--mostrar") comando = CMD_MOSTRAR;
            else if (s == "--ocultar") comando = CMD_OCULTAR;
            else if (s == "--alternar") comando = CMD_ALTERNAR;
            else if (s == "--config") comando = CMD_CONFIG;
        }

        bool esNueva;
        using (Mutex mutex = new Mutex(true, "Local\\" + TITULO, out esNueva))
        {
            if (!esNueva)
            {
                // Ya hay una instancia: le enviamos el comando y terminamos.
                IntPtr h = Win32.FindWindow(null, TITULO);
                if (h != IntPtr.Zero) Win32.PostMessage(h, WM_COMANDO, (IntPtr)comando, IntPtr.Zero);
                return;
            }
            if (comando == CMD_SALIR) return;

            Opciones op;
            try { op = Opciones.Leer(args); }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n\n" + Opciones.AYUDA, Program.NOMBRE, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (comando == CMD_OCULTAR) op.Oculto = true;
            if (comando == CMD_CONFIG) op.AbrirConfig = true;

            Application.EnableVisualStyles();
            MenuInicio.Asegurar();
            // El arranque con Windows apunta siempre a este .exe (por si la app se movió de carpeta).
            if (op.IniciarConWindows.HasValue) { try { Inicio.Establecer(op.IniciarConWindows.Value); } catch { } }
            else Inicio.Migrar();
            Application.Run(new Overlay(op));
        }
    }
}

class Opciones
{
    public string Ruta;
    public float Escala = 1f;
    public float Opacidad = 1f;
    public int PosV = 0, PosH = 2;          // vertical: 0 arriba, 1 centro, 2 abajo · horizontal: 0 izquierda, 1 centro, 2 derecha
    public bool PantallaCompleta = false;  // el reloj ocupa lo más posible de la pantalla
    public bool Oculto, Reloj, AbrirConfig;
    public bool Formato12 = false, Segundos = true, Fecha = true;
    public int Diseno = 0;
    public int ColorIdx = 0;              // índice de la paleta, o -1 para color personalizado
    public Color ColorPersonal = Color.Empty;
    public string Pantalla = "";           // "" = principal, "*" = todas, o el nombre del monitor (\\.\DISPLAY2)
    public Atajo[] Atajos = Acciones.PorDefecto();
    public List<Alarma> Alarmas = new List<Alarma>();
    public bool SonidoAlarma = true;
    public bool? IniciarConWindows;        // null = no está en config.ini (versiones anteriores)
    public List<ConexionCalendario> Calendarios = new List<ConexionCalendario>(); // calendarios sincronizados (enlaces iCal)
    public int AvisoCalendario = 10;       // minutos antes de cada evento en que suena el despertador (0 = sin aviso)
    public HashSet<string> EventosSilenciados = new HashSet<string>(); // eventos sin aviso (Clave del evento)

    public static readonly string[] VERTICAL = { "arriba", "centro", "abajo" }, HORIZONTAL = { "izquierda", "centro", "derecha" };

    public const string AYUDA =
        "Uso: RelojBackground.exe [imagen.gif|png | --reloj] [--12h] [--escala 1.0] [--opacidad 0.8] [--pos centro|arriba-derecha|abajo-centro|...] [--completa] [--oculto]\n" +
        "Con la app ya abierta: RelojBackground.exe [--alternar|--mostrar|--ocultar|--config|--salir]";

    // Si hay un config.ini junto al .exe, la configuración se guarda ahí (instalación "portátil");
    // si no, en %LOCALAPPDATA%\RelojBackground.
    public static string Carpeta
    {
        get
        {
            string junto = Path.GetDirectoryName(Application.ExecutablePath);
            if (File.Exists(Path.Combine(junto, "config.ini"))) return junto;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RelojBackground");
        }
    }

    // Carpeta de las primeras versiones (cuando la app se llamaba Overlay).
    static string CarpetaAntigua
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OverlayReloj"); }
    }

    public static string ArchivoConfig { get { return Path.Combine(Carpeta, "config.ini"); } }

    public static Opciones Leer(string[] args)
    {
        Opciones o = new Opciones();
        o.Cargar();
        bool posIndicada = false;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i].ToLowerInvariant();
            if (a == "--escala") o.Escala = ParseFloat(Siguiente(args, ref i));
            else if (a == "--opacidad") o.Opacidad = ParseFloat(Siguiente(args, ref i));
            else if (a == "--pos")
            {
                string p = Siguiente(args, ref i).ToLowerInvariant();
                if (!o.LeerPosicion(p)) throw new ArgumentException("Posición desconocida: " + p);
                posIndicada = true;
            }
            else if (a == "--oculto") o.Oculto = true;
            else if (a == "--reloj") o.Reloj = true;
            else if (a == "--12h") o.Formato12 = true;
            else if (a == "--completa") o.PantallaCompleta = true;
            else if (a == "--alternar" || a == "--mostrar" || a == "--ocultar" || a == "--config") { }
            else if (a.StartsWith("--")) throw new ArgumentException("Opción desconocida: " + args[i]);
            else o.Ruta = args[i];
        }

        // Sin imagen indicada se muestra el reloj.
        if (o.Ruta == null) o.Reloj = true;
        else if (!File.Exists(o.Ruta)) throw new FileNotFoundException("No se encuentra la imagen: " + o.Ruta);
        if (o.Reloj) o.Ruta = null;
        else if (!posIndicada) { o.PosV = 1; o.PosH = 1; } // las imágenes van al centro por defecto

        o.Limitar();
        return o;
    }

    public void Limitar()
    {
        Escala = Math.Max(0.2f, Math.Min(5f, Escala)); // de 20 % a 500 %
        Opacidad = Math.Max(0.1f, Math.Min(1f, Opacidad));
        PosV = Math.Max(0, Math.Min(2, PosV));
        PosH = Math.Max(0, Math.Min(2, PosH));
        if (Diseno < 0 || Diseno >= global::Reloj.Disenos.Length) Diseno = 0; // global:: porque el campo bool Reloj tapa a la clase
        if (ColorIdx >= Paleta.Nombres.Length || (ColorIdx < 0 && ColorPersonal.IsEmpty)) ColorIdx = 0;
    }

    public Opciones Clonar()
    {
        Opciones o = (Opciones)MemberwiseClone();
        o.Atajos = (Atajo[])Atajos.Clone();
        o.Alarmas = CopiarAlarmas(Alarmas);
        o.Calendarios = CopiarCalendarios(Calendarios);
        o.EventosSilenciados = new HashSet<string>(EventosSilenciados);
        return o;
    }

    static List<Alarma> CopiarAlarmas(List<Alarma> origen)
    {
        List<Alarma> l = new List<Alarma>();
        foreach (Alarma a in origen) l.Add(a.Copia());
        return l;
    }

    static List<ConexionCalendario> CopiarCalendarios(List<ConexionCalendario> origen)
    {
        List<ConexionCalendario> l = new List<ConexionCalendario>();
        foreach (ConexionCalendario c in origen) l.Add(c.Copia());
        return l;
    }

    public void CopiarDe(Opciones o)
    {
        Escala = o.Escala; Opacidad = o.Opacidad; PosV = o.PosV; PosH = o.PosH;
        PantallaCompleta = o.PantallaCompleta;
        Formato12 = o.Formato12; Segundos = o.Segundos; Fecha = o.Fecha;
        Diseno = o.Diseno; ColorIdx = o.ColorIdx; ColorPersonal = o.ColorPersonal;
        Pantalla = o.Pantalla;
        Atajos = (Atajo[])o.Atajos.Clone();
        Alarmas = CopiarAlarmas(o.Alarmas);
        SonidoAlarma = o.SonidoAlarma;
        Calendarios = CopiarCalendarios(o.Calendarios);
        AvisoCalendario = o.AvisoCalendario;
        EventosSilenciados = new HashSet<string>(o.EventosSilenciados);
    }

    // Configuración guardada en %LOCALAPPDATA%\RelojBackground\config.ini (clave=valor).
    void Cargar()
    {
        string antigua = Path.Combine(CarpetaAntigua, "config.ini");
        if (!File.Exists(ArchivoConfig) && File.Exists(antigua))
        {
            // Primera vez con el nombre nuevo: se trae la configuración de la versión anterior.
            try { Directory.CreateDirectory(Carpeta); File.Copy(antigua, ArchivoConfig); } catch { }
        }
        if (!File.Exists(ArchivoConfig)) return;
        foreach (string linea in File.ReadAllLines(ArchivoConfig))
        {
            int eq = linea.IndexOf('=');
            if (eq <= 0) continue;
            string k = linea.Substring(0, eq).Trim().ToLowerInvariant(), v = linea.Substring(eq + 1).Trim();
            try
            {
                switch (k)
                {
                    case "diseno": Diseno = int.Parse(v); break;
                    case "color":
                        if (v.StartsWith("#")) { ColorIdx = -1; ColorPersonal = ColorTranslator.FromHtml(v); }
                        else ColorIdx = int.Parse(v);
                        break;
                    case "formato12": Formato12 = v == "1"; break;
                    case "segundos": Segundos = v == "1"; break;
                    case "fecha": Fecha = v == "1"; break;
                    case "escala": Escala = ParseFloat(v); break;
                    case "opacidad": Opacidad = ParseFloat(v); break;
                    case "posicion": LeerPosicion(v); break;
                    case "completa": PantallaCompleta = v == "1"; break;
                    case "pantalla": Pantalla = v == "todas" ? "*" : v == "principal" ? "" : v; break;
                    case "alarma":
                        Alarma al = Alarma.Leer(v);
                        if (al != null) Alarmas.Add(al);
                        break;
                    case "sonido": SonidoAlarma = v == "1"; break;
                    case "inicio": IniciarConWindows = v == "1"; break;
                    case "calendario":
                        ConexionCalendario cal = ConexionCalendario.Leer(v);
                        if (cal != null) Calendarios.Add(cal);
                        break;
                    case "aviso_calendario": AvisoCalendario = Math.Max(0, Math.Min(240, int.Parse(v))); break;
                    case "silenciado": EventosSilenciados.Add(v); break;
                    default:
                        if (k.StartsWith("atajo."))
                        {
                            int i = Array.IndexOf(Acciones.Claves, k.Substring(6));
                            if (i >= 0) Atajos[i] = Atajo.Leer(v);
                        }
                        break;
                }
            }
            catch { } // una línea mal escrita no impide cargar las demás
        }
        Limitar();
    }

    public void Guardar()
    {
        Directory.CreateDirectory(Carpeta);
        CultureInfo inv = CultureInfo.InvariantCulture;
        string color = ColorIdx < 0
            ? "#" + ColorPersonal.R.ToString("X2") + ColorPersonal.G.ToString("X2") + ColorPersonal.B.ToString("X2")
            : ColorIdx.ToString(inv);
        List<string> lineas = new List<string>(new string[] {
            "diseno=" + Diseno,
            "color=" + color,
            "formato12=" + (Formato12 ? 1 : 0),
            "segundos=" + (Segundos ? 1 : 0),
            "fecha=" + (Fecha ? 1 : 0),
            "escala=" + Escala.ToString("0.###", inv),
            "opacidad=" + Opacidad.ToString("0.###", inv),
            "posicion=" + TextoPosicion(),
            "completa=" + (PantallaCompleta ? 1 : 0),
            "pantalla=" + (Pantalla == "*" ? "todas" : Pantalla == "" ? "principal" : Pantalla),
        });
        for (int i = 0; i < Acciones.Claves.Length; i++) lineas.Add("atajo." + Acciones.Claves[i] + "=" + Atajos[i].Texto());
        lineas.Add("sonido=" + (SonidoAlarma ? 1 : 0));
        if (IniciarConWindows.HasValue) lineas.Add("inicio=" + (IniciarConWindows.Value ? 1 : 0));
        lineas.Add("aviso_calendario=" + AvisoCalendario);
        foreach (ConexionCalendario c in Calendarios) lineas.Add("calendario=" + c.Texto());
        // Solo se recuerdan los silencios de eventos que aún no han pasado (la clave termina en la fecha yyyyMMddHHmm).
        string hoy = DateTime.Today.ToString("yyyyMMdd");
        foreach (string s in EventosSilenciados)
            if (s.Length >= 12 && string.CompareOrdinal(s.Substring(s.Length - 12, 8), hoy) >= 0) lineas.Add("silenciado=" + s);
        foreach (Alarma a in Alarmas) lineas.Add("alarma=" + a.Texto());
        File.WriteAllLines(ArchivoConfig, lineas.ToArray());
    }

    // Acepta "centro" o "vertical-horizontal", p. ej. "arriba-derecha", "abajo-centro", "centro-izquierda".
    public bool LeerPosicion(string s)
    {
        s = s.Trim().ToLowerInvariant();
        if (s == "centro") { PosV = 1; PosH = 1; return true; }
        string[] p = s.Split('-');
        if (p.Length != 2) return false;
        int v = Array.IndexOf(VERTICAL, p[0]), h = Array.IndexOf(HORIZONTAL, p[1]);
        if (v < 0 || h < 0) return false;
        PosV = v; PosH = h;
        return true;
    }

    public string TextoPosicion()
    {
        return PosV == 1 && PosH == 1 ? "centro" : VERTICAL[PosV] + "-" + HORIZONTAL[PosH];
    }

    static string Siguiente(string[] args, ref int i)
    {
        if (i + 1 >= args.Length) throw new ArgumentException("Falta el valor de " + args[i]);
        return args[++i];
    }

    static float ParseFloat(string s)
    {
        return float.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);
    }
}

// ---------- Atajos de teclado ----------

// Una combinación de teclas: modificadores (Ctrl, Alt, Mayús, Win) + una tecla. Tecla = None significa "sin atajo".
struct Atajo
{
    public uint Mods;
    public Keys Tecla;

    public Atajo(uint mods, Keys tecla) { Mods = mods; Tecla = tecla; }

    public bool Vacio { get { return Tecla == Keys.None; } }

    // Texto para config.ini, p. ej. "Ctrl+Alt+I" o "Ctrl+Shift+PageUp".
    public string Texto()
    {
        if (Vacio) return "";
        return Modificadores("Ctrl", "Alt", "Shift", "Win") + Tecla;
    }

    // Texto para mostrar al usuario, p. ej. "Ctrl+Alt+RePág".
    public string Mostrar()
    {
        if (Vacio) return "";
        return Modificadores("Ctrl", "Alt", "Mayús", "Win") + NombreTecla(Tecla);
    }

    public string MostrarModificadores() { return Modificadores("Ctrl", "Alt", "Mayús", "Win"); }

    string Modificadores(string ctrl, string alt, string shift, string win)
    {
        string s = "";
        if ((Mods & Win32.MOD_CONTROL) != 0) s += ctrl + "+";
        if ((Mods & Win32.MOD_ALT) != 0) s += alt + "+";
        if ((Mods & Win32.MOD_SHIFT) != 0) s += shift + "+";
        if ((Mods & Win32.MOD_WIN) != 0) s += win + "+";
        return s;
    }

    public static Atajo Leer(string s)
    {
        Atajo a = new Atajo();
        if (string.IsNullOrEmpty(s)) return a;
        string[] partes = s.Split('+');
        for (int i = 0; i < partes.Length - 1; i++)
        {
            switch (partes[i].Trim().ToLowerInvariant())
            {
                case "ctrl": a.Mods |= Win32.MOD_CONTROL; break;
                case "alt": a.Mods |= Win32.MOD_ALT; break;
                case "shift": a.Mods |= Win32.MOD_SHIFT; break;
                case "win": a.Mods |= Win32.MOD_WIN; break;
            }
        }
        try { a.Tecla = (Keys)Enum.Parse(typeof(Keys), partes[partes.Length - 1].Trim(), true); }
        catch { a.Tecla = Keys.None; }
        return a;
    }

    public static string NombreTecla(Keys k)
    {
        if (k >= Keys.D0 && k <= Keys.D9) return ((int)(k - Keys.D0)).ToString();
        if (k >= Keys.NumPad0 && k <= Keys.NumPad9) return "Num " + (int)(k - Keys.NumPad0);
        switch (k)
        {
            case Keys.PageUp: return "RePág";
            case Keys.PageDown: return "AvPág";
            case Keys.Home: return "Inicio";
            case Keys.End: return "Fin";
            case Keys.Insert: return "Insert";
            case Keys.Delete: return "Supr";
            case Keys.Up: return "Flecha arriba";
            case Keys.Down: return "Flecha abajo";
            case Keys.Left: return "Flecha izquierda";
            case Keys.Right: return "Flecha derecha";
            case Keys.Space: return "Espacio";
            case Keys.Enter: return "Intro";
            case Keys.Back: return "Retroceso";
            case Keys.Escape: return "Esc";
            case Keys.Pause: return "Pausa";
            case Keys.Oemplus: return "+";
            case Keys.OemMinus: return "-";
            case Keys.Oemcomma: return ",";
            case Keys.OemPeriod: return ".";
            case Keys.Add: return "Num +";
            case Keys.Subtract: return "Num -";
            case Keys.Multiply: return "Num *";
            case Keys.Divide: return "Num /";
            default: return k.ToString();
        }
    }
}

// Las acciones que se pueden asignar a un atajo.
static class Acciones
{
    public const int ALTERNAR = 0, CONFIG = 1, POSICION = 2, GRANDE = 3, PEQUENO = 4, OPACO = 5, TRANSPARENTE = 6, SALIR = 7,
                     CRONO = 8, CRONO_REINICIAR = 9;

    public static readonly string[] Nombres = {
        "Mostrar / ocultar el reloj", "Abrir la configuración", "Cambiar de posición", "Hacer más grande",
        "Hacer más pequeño", "Más opaco", "Más transparente", "Cerrar la aplicación",
        "Iniciar / pausar el cronómetro", "Reiniciar el cronómetro" };

    // Nombre de cada acción en config.ini (atajo.<clave>=...).
    public static readonly string[] Claves = { "alternar", "config", "posicion", "grande", "pequeno", "opaco", "transparente", "salir",
                                               "crono", "crono_reiniciar" };

    // Las que se repiten al mantener pulsada la combinación.
    public static readonly bool[] Repite = { false, false, false, true, true, true, true, false, false, false };

    // Por defecto solo Ctrl+Alt+I y Ctrl+Alt+C; el resto sin atajo.
    public static Atajo[] PorDefecto()
    {
        Atajo[] a = new Atajo[Nombres.Length];
        a[ALTERNAR] = new Atajo(Win32.MOD_CONTROL | Win32.MOD_ALT, Keys.I);
        a[CONFIG] = new Atajo(Win32.MOD_CONTROL | Win32.MOD_ALT, Keys.C);
        return a;
    }

    public static string Firma(Atajo[] atajos)
    {
        string s = "";
        foreach (Atajo a in atajos) s += a.Texto() + "|";
        return s;
    }

    // Frase corta con los dos atajos principales, para avisos.
    public static string Resumen(Atajo[] atajos)
    {
        List<string> partes = new List<string>();
        if (!atajos[ALTERNAR].Vacio) partes.Add(atajos[ALTERNAR].Mostrar() + " muestra u oculta el reloj");
        if (!atajos[CONFIG].Vacio) partes.Add(atajos[CONFIG].Mostrar() + " abre la configuración");
        return partes.Count > 0 ? string.Join("\n", partes.ToArray()) : "Doble clic en el icono del reloj abre la configuración";
    }
}

// ---------- Alarmas ----------

// Una alarma: hora, si está activa y qué días suena.
class Alarma
{
    public const int TODOS = 0, LABORABLES = 1, HOY = 2, RANGO = 3;
    public static readonly string[] MODOS = { "Todos los días", "Días laborables (lun–vie)", "Solo hoy", "Rango de fechas…" };

    public int Hora = 7, Minuto = 0;
    public bool Activa = true;
    public int Modo = TODOS;
    public DateTime Fecha = DateTime.Today;                         // "Solo hoy": el día en que se eligió
    public DateTime Desde = DateTime.Today, Hasta = DateTime.Today.AddDays(6); // "Rango de fechas" (ambos incluidos)

    public Alarma Copia() { return (Alarma)MemberwiseClone(); }

    public string HoraTexto() { return Hora.ToString("00") + ":" + Minuto.ToString("00"); }

    // ¿Le toca sonar ese día?
    public bool TocaEl(DateTime dia)
    {
        switch (Modo)
        {
            case LABORABLES: return dia.DayOfWeek != DayOfWeek.Saturday && dia.DayOfWeek != DayOfWeek.Sunday;
            case HOY: return dia.Date == Fecha.Date;
            case RANGO: return dia.Date >= Desde.Date && dia.Date <= Hasta.Date;
            default: return true;
        }
    }

    // Las de un solo día se desactivan al sonar; las demás siguen activas.
    public bool UnaSolaVez { get { return Modo == HOY; } }

    static readonly System.Globalization.CultureInfo ES = new System.Globalization.CultureInfo("es-ES");

    // "2 nov – 8 nov 2026" (o "2 nov 2026 – 5 ene 2027" si cambia el año).
    public string RangoTexto()
    {
        string a = Desde.ToString("d MMM", ES).Replace(".", ""), b = Hasta.ToString("d MMM yyyy", ES).Replace(".", "");
        if (Desde.Year != Hasta.Year) a += " " + Desde.Year;
        return a + "  –  " + b;
    }

    // En config.ini: alarma=07:30,1,3,2026-11-02,2026-11-02,2026-11-08  (hora, activa, modo, fecha, desde, hasta)
    public string Texto()
    {
        return HoraTexto() + "," + (Activa ? 1 : 0) + "," + Modo + "," + Fecha.ToString("yyyy-MM-dd") + "," +
               Desde.ToString("yyyy-MM-dd") + "," + Hasta.ToString("yyyy-MM-dd");
    }

    public static Alarma Leer(string s)
    {
        try
        {
            string[] p = s.Split(',');
            string[] hm = p[0].Split(':');
            Alarma a = new Alarma();
            a.Hora = Math.Max(0, Math.Min(23, int.Parse(hm[0])));
            a.Minuto = Math.Max(0, Math.Min(59, int.Parse(hm[1])));
            if (p.Length > 1) a.Activa = p[1].Trim() == "1";
            if (p.Length >= 6)
            {
                a.Modo = Math.Max(TODOS, Math.Min(RANGO, int.Parse(p[2])));
                a.Fecha = Dia(p[3]);
                a.Desde = Dia(p[4]);
                a.Hasta = Dia(p[5]);
            }
            else if (p.Length > 2) a.Modo = p[2].Trim() == "1" ? TODOS : HOY; // versiones anteriores: 1 = todos los días
            return a;
        }
        catch { return null; }
    }

    static DateTime Dia(string s)
    {
        return DateTime.ParseExact(s.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }
}

// Sonido de la alarma: dos pitidos cortos y una pausa, generado en memoria (no necesita archivos).
static class Sonidos
{
    public static MemoryStream Alarma()
    {
        const int muestras = 22050;
        List<short> datos = new List<short>();
        for (int r = 0; r < 2; r++)
        {
            Tono(datos, muestras, 988, 0.11);
            Silencio(datos, muestras, 0.07);
        }
        Silencio(datos, muestras, 0.55);

        MemoryStream ms = new MemoryStream();
        BinaryWriter w = new BinaryWriter(ms);
        int bytes = datos.Count * 2;
        w.Write(new char[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
        w.Write(new char[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(muestras); w.Write(muestras * 2); w.Write((short)2); w.Write((short)16);
        w.Write(new char[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
        foreach (short m in datos) w.Write(m);
        w.Flush();
        ms.Position = 0;
        return ms;
    }

    static void Tono(List<short> d, int muestras, double hz, double segundos)
    {
        int n = (int)(muestras * segundos), rampa = muestras / 125; // 8 ms de entrada y salida suave
        for (int i = 0; i < n; i++)
        {
            double env = Math.Min(1, Math.Min(i, n - i) / (double)rampa);
            double t = i / (double)muestras;
            double v = Math.Sin(2 * Math.PI * hz * t) * 0.8 + Math.Sin(4 * Math.PI * hz * t) * 0.2;
            d.Add((short)(v * env * 0.45 * short.MaxValue));
        }
    }

    static void Silencio(List<short> d, int muestras, double segundos)
    {
        int n = (int)(muestras * segundos);
        for (int i = 0; i < n; i++) d.Add(0);
    }
}

// Icono de la app (reloj.ico va incrustado en el .exe).
static class Iconos
{
    public static readonly Icon Pequeno = Cargar(SystemInformation.SmallIconSize.Width);
    public static readonly Icon Grande = Cargar(32);

    static Icon Cargar(int lado)
    {
        try
        {
            using (Stream s = typeof(Iconos).Assembly.GetManifestResourceStream("reloj.ico"))
                if (s != null) return new Icon(s, lado, lado);
        }
        catch { }
        return SystemIcons.Application;
    }
}

// Acceso "RelojBackground" en el menú Inicio (se crea la primera vez que se abre la app).
static class MenuInicio
{
    static string Ruta
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Program.NOMBRE + ".lnk"); }
    }

    public static void Asegurar()
    {
        try
        {
            Type tipo = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(tipo);
            object acceso = tipo.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { Ruta });
            Type ta = acceso.GetType();
            // Si ya existe y apunta a este .exe, no hay nada que hacer; si la app se movió, se actualiza.
            string actual = (string)ta.InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, acceso, null);
            if (File.Exists(Ruta) && string.Equals(actual, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase)) return;
            System.Reflection.BindingFlags poner = System.Reflection.BindingFlags.SetProperty;
            ta.InvokeMember("TargetPath", poner, null, acceso, new object[] { Application.ExecutablePath });
            ta.InvokeMember("Arguments", poner, null, acceso, new object[] { "--config" }); // abrirlo desde Inicio abre la configuración
            ta.InvokeMember("WorkingDirectory", poner, null, acceso, new object[] { Path.GetDirectoryName(Application.ExecutablePath) });
            ta.InvokeMember("Description", poner, null, acceso, new object[] { "Reloj transparente, alarmas y cronómetro" });
            ta.InvokeMember("IconLocation", poner, null, acceso, new object[] { Application.ExecutablePath + ",0" });
            ta.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, acceso, null);
        }
        catch { } // sin acceso en el menú Inicio la app funciona igual
    }
}

// ---------- Calendarios (enlaces iCal de Google, Outlook, iCloud u otros) ----------

// Un calendario sincronizado. La URL es la dirección privada en formato iCal: funciona como una contraseña,
// por eso solo se guarda en config.ini (que no se sube a GitHub).
class ConexionCalendario
{
    public static readonly string[] SERVICIOS = { "Google", "Outlook", "iCloud", "Otro" };

    public string Servicio = "Google", Nombre = "Google", Url = "";

    public ConexionCalendario Copia() { return (ConexionCalendario)MemberwiseClone(); }

    // En config.ini: calendario=Google|Trabajo|https://calendar.google.com/calendar/ical/.../basic.ics
    public string Texto() { return Servicio + "|" + Nombre.Replace("|", " ") + "|" + Url; }

    public static ConexionCalendario Leer(string s)
    {
        string[] p = s.Split(new char[] { '|' }, 3);
        if (p.Length < 3 || p[2].Trim() == "") return null;
        ConexionCalendario c = new ConexionCalendario();
        c.Servicio = p[0].Trim();
        c.Nombre = p[1].Trim();
        c.Url = p[2].Trim();
        return c;
    }
}

class EventoCalendario
{
    public string Uid = "", Titulo = "", Fuente = "";
    public DateTime Inicio, Fin;
    public bool TodoElDia;

    // Identifica una aparición concreta del evento (los que se repiten tienen el mismo Uid).
    public string Clave { get { return Uid + "|" + Inicio.ToString("yyyyMMddHHmm"); } }
}

// Lee calendarios en formato iCal (.ics): eventos sueltos, de todo el día y que se repiten (RRULE).
static class ICal
{
    static ICal()
    {
        // Google, Outlook e iCloud solo aceptan TLS 1.2 o superior.
        try { System.Net.ServicePointManager.SecurityProtocol |= (System.Net.SecurityProtocolType)3072; } catch { }
    }

    // webcal:// (iCloud y otros) es lo mismo que https://
    public static string Normalizar(string url)
    {
        url = (url ?? "").Trim();
        if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase)) url = "https://" + url.Substring(9);
        return url;
    }

    public static List<EventoCalendario> Descargar(string url, string fuente, DateTime desde, DateTime hasta)
    {
        url = Normalizar(url);
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new Exception("El enlace debe empezar por https:// o webcal://");
        string texto;
        try
        {
            System.Net.HttpWebRequest req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
            req.UserAgent = Program.NOMBRE;
            req.Timeout = 20000;
            req.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
            using (System.Net.WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), System.Text.Encoding.UTF8))
                texto = sr.ReadToEnd();
        }
        catch (System.Net.WebException ex)
        {
            System.Net.HttpWebResponse r = ex.Response as System.Net.HttpWebResponse;
            if (r != null && ((int)r.StatusCode == 404 || (int)r.StatusCode == 403 || (int)r.StatusCode == 401))
                throw new Exception("el servicio no encontró el calendario. Revisa que el enlace esté completo y siga activo.");
            throw new Exception("no se pudo conectar (" + ex.Message.TrimEnd('.') + ").");
        }
        if (texto.IndexOf("BEGIN:VCALENDAR", StringComparison.OrdinalIgnoreCase) < 0)
            throw new Exception("el enlace no es un calendario iCal (.ics). Copia la dirección en formato iCal.");
        return Leer(texto, fuente, desde, hasta);
    }

    // Eventos (y repeticiones) que empiezan entre desde y hasta.
    public static List<EventoCalendario> Leer(string texto, string fuente, DateTime desde, DateTime hasta)
    {
        // Las líneas largas vienen partidas: las que empiezan con espacio o tabulador continúan la anterior.
        List<string> lineas = new List<string>();
        foreach (string l in texto.Replace("\r\n", "\n").Split('\n'))
        {
            if ((l.StartsWith(" ") || l.StartsWith("\t")) && lineas.Count > 0) lineas[lineas.Count - 1] += l.Substring(1);
            else lineas.Add(l.TrimEnd('\r'));
        }

        List<Dictionary<string, Propiedad>> eventos = new List<Dictionary<string, Propiedad>>();
        List<List<Propiedad>> exdates = new List<List<Propiedad>>();
        Dictionary<string, Propiedad> actual = null;
        List<Propiedad> exActual = null;
        int anidado = 0; // VALARM y otros bloques dentro del evento
        foreach (string l in lineas)
        {
            if (l.Equals("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase)) { actual = new Dictionary<string, Propiedad>(); exActual = new List<Propiedad>(); anidado = 0; continue; }
            if (actual == null) continue;
            if (l.Equals("END:VEVENT", StringComparison.OrdinalIgnoreCase)) { eventos.Add(actual); exdates.Add(exActual); actual = null; continue; }
            if (l.StartsWith("BEGIN:", StringComparison.OrdinalIgnoreCase)) { anidado++; continue; }
            if (l.StartsWith("END:", StringComparison.OrdinalIgnoreCase)) { anidado--; continue; }
            if (anidado > 0) continue;
            Propiedad p = Propiedad.Leer(l);
            if (p == null) continue;
            if (p.Nombre == "EXDATE") exActual.Add(p);
            else if (!actual.ContainsKey(p.Nombre)) actual[p.Nombre] = p;
        }

        // Apariciones modificadas de eventos que se repiten (se quitan de la serie y se añaden como evento propio).
        HashSet<string> modificadas = new HashSet<string>();
        foreach (Dictionary<string, Propiedad> e in eventos)
            if (e.ContainsKey("RECURRENCE-ID") && e.ContainsKey("UID"))
            {
                bool dia;
                modificadas.Add(e["UID"].Valor + "|" + Fecha(e["RECURRENCE-ID"], out dia).ToString("yyyyMMddHHmm"));
            }

        List<EventoCalendario> res = new List<EventoCalendario>();
        for (int i = 0; i < eventos.Count; i++)
        {
            Dictionary<string, Propiedad> e = eventos[i];
            if (!e.ContainsKey("DTSTART")) continue;
            if (e.ContainsKey("STATUS") && e["STATUS"].Valor.ToUpperInvariant() == "CANCELLED") continue;
            bool todoElDia;
            DateTime inicio = Fecha(e["DTSTART"], out todoElDia);
            bool d2;
            DateTime fin = e.ContainsKey("DTEND") ? Fecha(e["DTEND"], out d2) : (todoElDia ? inicio.AddDays(1) : inicio.AddHours(1));
            TimeSpan duracion = fin > inicio ? fin - inicio : TimeSpan.Zero;
            string uid = e.ContainsKey("UID") ? e["UID"].Valor : Guid.NewGuid().ToString();
            string titulo = e.ContainsKey("SUMMARY") ? Desescapar(e["SUMMARY"].Valor) : "(Sin título)";

            HashSet<DateTime> excluidas = new HashSet<DateTime>();
            foreach (Propiedad ex in exdates[i])
                foreach (string v in ex.Valor.Split(','))
                {
                    bool d3;
                    excluidas.Add(Fecha(new Propiedad { Nombre = "EXDATE", Parametros = ex.Parametros, Valor = v }, out d3));
                }

            List<DateTime> apariciones = e.ContainsKey("RRULE") && !e.ContainsKey("RECURRENCE-ID")
                ? Repeticiones(inicio, e["RRULE"].Valor, desde.AddDays(-1), hasta)
                : new List<DateTime> { inicio };

            foreach (DateTime a in apariciones)
            {
                if (excluidas.Contains(a)) continue;
                if (!e.ContainsKey("RECURRENCE-ID") && e.ContainsKey("RRULE") && modificadas.Contains(uid + "|" + a.ToString("yyyyMMddHHmm"))) continue;
                DateTime f = a + duracion;
                if (f <= desde || a >= hasta) continue; // fuera del rango (los que ya empezaron y siguen, se muestran)
                EventoCalendario ev = new EventoCalendario();
                ev.Uid = uid;
                ev.Titulo = titulo;
                ev.Fuente = fuente;
                ev.Inicio = a;
                ev.Fin = f;
                ev.TodoElDia = todoElDia;
                res.Add(ev);
            }
        }
        res.Sort(delegate(EventoCalendario a, EventoCalendario b) { return a.Inicio.CompareTo(b.Inicio); });
        return res;
    }

    // Una línea "NOMBRE;PARAM=valor:VALOR".
    class Propiedad
    {
        public string Nombre = "", Valor = "";
        public Dictionary<string, string> Parametros = new Dictionary<string, string>();

        public static Propiedad Leer(string l)
        {
            int dosPuntos = -1;
            bool comillas = false;
            for (int i = 0; i < l.Length; i++)
            {
                if (l[i] == '"') comillas = !comillas;
                if (l[i] == ':' && !comillas) { dosPuntos = i; break; }
            }
            if (dosPuntos <= 0) return null;
            Propiedad p = new Propiedad();
            string[] partes = l.Substring(0, dosPuntos).Split(';');
            p.Nombre = partes[0].ToUpperInvariant();
            for (int i = 1; i < partes.Length; i++)
            {
                int igual = partes[i].IndexOf('=');
                if (igual > 0) p.Parametros[partes[i].Substring(0, igual).ToUpperInvariant()] = partes[i].Substring(igual + 1).Trim('"');
            }
            p.Valor = l.Substring(dosPuntos + 1);
            return p;
        }
    }

    // Fecha u hora en hora local. Admite fechas (todo el día), UTC (…Z) y horas con zona horaria (TZID).
    static DateTime Fecha(Propiedad p, out bool soloFecha)
    {
        string v = p.Valor.Trim();
        System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
        soloFecha = v.Length == 8 || (p.Parametros.ContainsKey("VALUE") && p.Parametros["VALUE"].ToUpperInvariant() == "DATE");
        if (soloFecha) return DateTime.ParseExact(v.Substring(0, 8), "yyyyMMdd", inv);
        bool utc = v.EndsWith("Z");
        DateTime d = DateTime.ParseExact(v.TrimEnd('Z').Substring(0, 15), "yyyyMMdd'T'HHmmss", inv);
        if (utc) return DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime();
        string tz;
        if (p.Parametros.TryGetValue("TZID", out tz))
        {
            TimeZoneInfo zona = Zona(tz);
            if (zona != null)
                try { return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(d, DateTimeKind.Unspecified), zona, TimeZoneInfo.Local); }
                catch { }
        }
        return d; // sin zona: hora local
    }

    // Zonas horarias IANA (las que usan los calendarios) → nombres de Windows. Las más comunes.
    static TimeZoneInfo Zona(string iana)
    {
        string[,] mapa = {
            { "America/Lima", "SA Pacific Standard Time" }, { "America/Bogota", "SA Pacific Standard Time" },
            { "America/Guayaquil", "SA Pacific Standard Time" }, { "America/Panama", "SA Pacific Standard Time" },
            { "America/Mexico_City", "Central Standard Time (Mexico)" }, { "America/Santiago", "Pacific SA Standard Time" },
            { "America/Argentina/Buenos_Aires", "Argentina Standard Time" }, { "America/Buenos_Aires", "Argentina Standard Time" },
            { "America/Caracas", "Venezuela Standard Time" }, { "America/La_Paz", "SA Western Standard Time" },
            { "America/Sao_Paulo", "E. South America Standard Time" }, { "America/Montevideo", "Montevideo Standard Time" },
            { "America/Asuncion", "Paraguay Standard Time" }, { "America/New_York", "Eastern Standard Time" },
            { "America/Chicago", "Central Standard Time" }, { "America/Denver", "Mountain Standard Time" },
            { "America/Los_Angeles", "Pacific Standard Time" }, { "Europe/Madrid", "Romance Standard Time" },
            { "Europe/London", "GMT Standard Time" }, { "Europe/Paris", "Romance Standard Time" },
            { "Europe/Berlin", "W. Europe Standard Time" }, { "UTC", "UTC" }, { "Etc/UTC", "UTC" }, { "GMT", "UTC" },
        };
        for (int i = 0; i < mapa.GetLength(0); i++)
            if (string.Equals(mapa[i, 0], iana, StringComparison.OrdinalIgnoreCase))
                try { return TimeZoneInfo.FindSystemTimeZoneById(mapa[i, 1]); } catch { return null; }
        try { return TimeZoneInfo.FindSystemTimeZoneById(iana); } catch { return null; } // Outlook usa nombres de Windows
    }

    static string Desescapar(string s)
    {
        return s.Replace("\\n", " ").Replace("\\N", " ").Replace("\\,", ",").Replace("\\;", ";").Replace("\\\\", "\\").Trim();
    }

    static readonly string[] DIAS = { "SU", "MO", "TU", "WE", "TH", "FR", "SA" }; // como DayOfWeek

    // Fechas de un evento que se repite (RRULE: FREQ, INTERVAL, COUNT, UNTIL, BYDAY, BYMONTHDAY), hasta "hasta".
    static List<DateTime> Repeticiones(DateTime inicio, string regla, DateTime desde, DateTime hasta)
    {
        Dictionary<string, string> r = new Dictionary<string, string>();
        foreach (string parte in regla.Split(';'))
        {
            int igual = parte.IndexOf('=');
            if (igual > 0) r[parte.Substring(0, igual).ToUpperInvariant()] = parte.Substring(igual + 1);
        }
        string frec = r.ContainsKey("FREQ") ? r["FREQ"].ToUpperInvariant() : "DAILY";
        int intervalo = r.ContainsKey("INTERVAL") ? Math.Max(1, int.Parse(r["INTERVAL"])) : 1;
        int cuenta = r.ContainsKey("COUNT") ? int.Parse(r["COUNT"]) : int.MaxValue;
        DateTime limite = hasta;
        if (r.ContainsKey("UNTIL"))
        {
            bool d;
            DateTime u = Fecha(new Propiedad { Nombre = "UNTIL", Valor = r["UNTIL"] }, out d);
            if (d) u = u.AddDays(1).AddTicks(-1);
            if (u < limite) limite = u;
        }
        List<string> porDia = r.ContainsKey("BYDAY") ? new List<string>(r["BYDAY"].ToUpperInvariant().Split(',')) : new List<string>();
        List<int> porDiaMes = new List<int>();
        if (r.ContainsKey("BYMONTHDAY")) foreach (string s in r["BYMONTHDAY"].Split(',')) porDiaMes.Add(int.Parse(s));

        List<DateTime> res = new List<DateTime>();
        int n = 0;
        TimeSpan hora = inicio.TimeOfDay;
        for (int paso = 0; paso < 2000 && n < cuenta; paso++)
        {
            List<DateTime> candidatos = new List<DateTime>();
            if (frec == "DAILY") candidatos.Add(inicio.AddDays(paso * intervalo));
            else if (frec == "WEEKLY")
            {
                DateTime lunes = inicio.Date.AddDays(-(((int)inicio.DayOfWeek + 6) % 7)).AddDays(7 * paso * intervalo);
                if (porDia.Count == 0) candidatos.Add(inicio.AddDays(7 * paso * intervalo));
                else for (int k = 0; k < 7; k++)
                    {
                        DateTime dia = lunes.AddDays(k);
                        if (porDia.Contains(DIAS[(int)dia.DayOfWeek])) candidatos.Add(dia + hora);
                    }
            }
            else if (frec == "MONTHLY")
            {
                DateTime mes = new DateTime(inicio.Year, inicio.Month, 1).AddMonths(paso * intervalo);
                if (porDia.Count > 0)
                    foreach (string bd in porDia) { DateTime x; if (DiaN(mes, bd, out x)) candidatos.Add(x + hora); }
                else foreach (int dm in porDiaMes.Count > 0 ? porDiaMes : new List<int> { inicio.Day })
                    {
                        int dias = DateTime.DaysInMonth(mes.Year, mes.Month);
                        int dd = dm > 0 ? dm : dias + dm + 1;
                        if (dd >= 1 && dd <= dias) candidatos.Add(new DateTime(mes.Year, mes.Month, dd) + hora);
                    }
            }
            else if (frec == "YEARLY")
            {
                int anio = inicio.Year + paso * intervalo;
                if (inicio.Month == 2 && inicio.Day == 29 && !DateTime.IsLeapYear(anio)) continue;
                candidatos.Add(new DateTime(anio, inicio.Month, inicio.Day) + hora);
            }
            else break;

            candidatos.Sort();
            bool pasado = false;
            foreach (DateTime c in candidatos)
            {
                if (c < inicio) continue;
                if (c > limite || n >= cuenta) { pasado = true; break; }
                n++;
                if (c >= desde) res.Add(c);
            }
            if (pasado) break;
        }
        return res;
    }

    // "2TU" = segundo martes del mes, "-1FR" = último viernes, "MO" = (en MONTHLY sin número) primer lunes.
    static bool DiaN(DateTime mes, string bd, out DateTime dia)
    {
        dia = mes;
        string codigo = bd.Length >= 2 ? bd.Substring(bd.Length - 2) : bd;
        int dow = Array.IndexOf(DIAS, codigo);
        if (dow < 0) return false;
        int num = 1;
        if (bd.Length > 2 && !int.TryParse(bd.Substring(0, bd.Length - 2), out num)) return false;
        int dias = DateTime.DaysInMonth(mes.Year, mes.Month);
        if (num > 0)
        {
            DateTime d = mes;
            while ((int)d.DayOfWeek != dow) d = d.AddDays(1);
            d = d.AddDays(7 * (num - 1));
            if (d.Month != mes.Month) return false;
            dia = d;
        }
        else
        {
            DateTime d = new DateTime(mes.Year, mes.Month, dias);
            while ((int)d.DayOfWeek != dow) d = d.AddDays(-1);
            d = d.AddDays(7 * (num + 1));
            if (d.Month != mes.Month) return false;
            dia = d;
        }
        return true;
    }
}

// ---------- Colores ----------

static class Paleta
{
    public static readonly string[] Nombres =
        { "Verde agua", "Azul hielo", "Morado", "Coral", "Ámbar", "Rosa", "Rojo", "Verde lima", "Blanco", "Arcoíris" };
    public const int ARCOIRIS = 9;

    // Por cada paleta: color principal, secundario y terciario (los usa el diseño "Tres anillos").
    static readonly string[][] HEX = {
        new string[] { "#5DCAA5", "#85B7EB", "#F0997B" },
        new string[] { "#85B7EB", "#B5D4F4", "#378ADD" },
        new string[] { "#AFA9EC", "#ED93B1", "#7F77DD" },
        new string[] { "#F0997B", "#FAC775", "#ED93B1" },
        new string[] { "#EF9F27", "#FAC775", "#F0997B" },
        new string[] { "#ED93B1", "#AFA9EC", "#F4C0D1" },
        new string[] { "#E24B4A", "#F09595", "#EF9F27" },
        new string[] { "#97C459", "#C0DD97", "#5DCAA5" },
        new string[] { "#FFFFFF", "#D3D1C7", "#B4B2A9" },
    };

    public static Color Principal(int i) { return ColorTranslator.FromHtml(HEX[i][0]); }

    public static Color[] Colores(Opciones o, DateTime t)
    {
        if (o.ColorIdx < 0)
        {
            Color a = o.ColorPersonal;
            double h = a.GetHue(), s = Math.Max(0.35, a.GetSaturation());
            return new Color[] { a, Mezclar(a, Color.White, 0.4), HSV(h + 40, s, 1) };
        }
        if (o.ColorIdx == ARCOIRIS)
        {
            double h = t.Minute * 6 + t.Second + t.Millisecond / 1000.0; // da una vuelta de color por hora
            return new Color[] { HSV(h + 190, 0.6, 1), HSV(h + 250, 0.5, 1), HSV(h + 130, 0.6, 1) };
        }
        string[] p = HEX[o.ColorIdx];
        return new Color[] { ColorTranslator.FromHtml(p[0]), ColorTranslator.FromHtml(p[1]), ColorTranslator.FromHtml(p[2]) };
    }

    static Color Mezclar(Color a, Color b, double t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    public static Color HSV(double h, double s, double v)
    {
        h = ((h % 360) + 360) % 360;
        int i = (int)(h / 60) % 6;
        double f = h / 60 - Math.Floor(h / 60);
        double p = v * (1 - s), q = v * (1 - f * s), u = v * (1 - (1 - f) * s);
        double r, g, b;
        switch (i)
        {
            case 0: r = v; g = u; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = u; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = u; g = p; b = v; break;
            default: r = v; g = p; b = q; break;
        }
        return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
    }
}

// ---------- Diseños del reloj ----------

class Parte
{
    public string T; public float Tam; public int Peso; public Color C;
    public Parte(string t, float tam, int peso, Color c) { T = t; Tam = tam; Peso = peso; C = c; }
}

static class Reloj
{
    public static readonly string[] Disenos = { "Anillo", "Analógico", "Minimalista", "Píldora compacta", "Tres anillos", "Tarjetas" };
    public const int NORMAL = 0, SEMI = 1, FINA = 2;

    static readonly CultureInfo ES = new CultureInfo("es-ES");
    static readonly Color FONDO = Color.FromArgb(140, 12, 16, 28);
    static readonly Color TENUE = Color.FromArgb(230, 224, 230, 245);
    static readonly Color BLANCO = Color.White;
    static readonly Dictionary<int, FontFamily> familias = new Dictionary<int, FontFamily>();

    // Tamaño del lienzo de cada diseño a escala 1.
    public static Size Base(int diseno)
    {
        switch (diseno)
        {
            case 2: return new Size(460, 220); // más ancho: los segundos van junto a los minutos
            case 3: return new Size(380, 120);
            case 5: return new Size(380, 210);
            default: return new Size(280, 280);
        }
    }

    // Dibuja el diseño elegido en coordenadas del lienzo base (el llamador aplica la escala).
    public static void Dibujar(Graphics g, Opciones o, DateTime t)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color[] c = Paleta.Colores(o, t);
        switch (o.Diseno)
        {
            case 1: Analogico(g, o, t, c); break;
            case 2: Minimalista(g, o, t, c); break;
            case 3: Pildora(g, o, t, c); break;
            case 4: TresAnillos(g, o, t, c); break;
            case 5: Tarjetas(g, o, t, c); break;
            default: Anillo(g, o, t, c); break;
        }
    }

    static void Anillo(Graphics g, Opciones o, DateTime t, Color[] c)
    {
        Disco(g, 140, 140, 128, FONDO);
        Marcas(g, 140, 140, 98, 102, 107);
        ArcoProgreso(g, 140, 140, 118, 7, Segundo(t) / 60f, c[0], true);

        List<Parte> abajo = new List<Parte>();
        if (o.Segundos) abajo.Add(new Parte(t.ToString("ss", ES), 20, NORMAL, c[0]));
        if (o.Formato12) abajo.Add(new Parte(AmPm(t), 16, NORMAL, TENUE));
        bool f = o.Fecha, s = abajo.Count > 0;
        float y = f && s ? 136 : f ? 150 : s ? 128 : 140;

        if (f) Linea(g, 140, y - 52, new Parte(FechaLarga(t), 15, NORMAL, TENUE));
        Linea(g, 140, y, new Parte(Hora(o, t), 66, SEMI, BLANCO));
        if (s) Linea(g, 140, y + 54, abajo.ToArray());
    }

    static void Analogico(Graphics g, Opciones o, DateTime t, Color[] c)
    {
        Disco(g, 140, 140, 128, FONDO);
        using (Pen borde = new Pen(Color.FromArgb(180, c[0]), 2)) g.DrawEllipse(borde, 12, 12, 256, 256);
        Marcas(g, 140, 140, 108, 112, 118);
        for (int i = 1; i <= 12; i++)
        {
            double a = i * Math.PI / 6;
            Linea(g, 140 + 82 * (float)Math.Sin(a), 140 - 82 * (float)Math.Cos(a), new Parte(i.ToString(), 18, NORMAL, BLANCO));
        }
        if (o.Fecha) Linea(g, 140, 186, new Parte(FechaCorta(t), 14, NORMAL, TENUE));

        float seg = Segundo(t), min = t.Minute + seg / 60f, hora = t.Hour % 12 + min / 60f;
        Aguja(g, hora * 30, 58, 8, BLANCO);
        Aguja(g, min * 6, 88, 5, BLANCO);
        if (o.Segundos) Aguja(g, seg * 6, 100, 2, c[0]);
        using (SolidBrush b = new SolidBrush(c[0])) g.FillEllipse(b, 134, 134, 12, 12);
    }

    static void Minimalista(Graphics g, Opciones o, DateTime t, Color[] c)
    {
        // Hora y minutos grandes; a su derecha una columna con los segundos arriba (a la altura de la parte
        // superior de las cifras) y "a. m./p. m." abajo (sobre la misma línea base). Debajo, la fecha.
        Parte hora = new Parte(Hora(o, t), 120, FINA, BLANCO);
        Parte seg = o.Segundos ? new Parte(":" + t.ToString("ss", ES), 46, FINA, BLANCO) : null;
        Parte ampm = o.Formato12 ? new Parte(AmPm(t), 46, FINA, TENUE) : null;

        float anchoHora = AnchoTexto(hora);
        float anchoColumna = Math.Max(seg != null ? AnchoTexto(seg) : 0, ampm != null ? AnchoTexto(ampm) : 0);
        float hueco = anchoColumna > 0 ? 10 : 0;
        float x = 230 - (anchoHora + hueco + anchoColumna) / 2;
        float baseY = (o.Fecha ? 92 : 110) + ALTO_CIFRAS / 2 * hora.Tam; // centro de las cifras en 92 (o 110)
        float tope = baseY - ALTO_CIFRAS * hora.Tam;

        TextoEn(g, hora, x, baseY);
        if (seg != null) TextoEn(g, seg, x + anchoHora + hueco, tope + ALTO_CIFRAS * seg.Tam);
        if (ampm != null) TextoEn(g, ampm, x + anchoHora + hueco, baseY);
        if (o.Fecha) Linea(g, 230, 172, new Parte(FechaLarga(t), 22, NORMAL, TENUE));
    }

    static void Pildora(Graphics g, Opciones o, DateTime t, Color[] c)
    {
        using (GraphicsPath gp = Redondeado(6, 18, 368, 84, 42))
        {
            using (SolidBrush b = new SolidBrush(FONDO)) g.FillPath(b, gp);
            using (Pen p = new Pen(Color.FromArgb(130, c[0]), 1.5f)) g.DrawPath(p, gp);
        }
        ArcoProgreso(g, 56, 60, 20, 5, Segundo(t) / 60f, c[0], false);

        List<Parte> hora = new List<Parte>();
        hora.Add(new Parte(Hora(o, t), 44, SEMI, BLANCO));
        if (o.Segundos) hora.Add(new Parte(":" + t.ToString("ss", ES), 26, SEMI, c[0]));
        if (o.Formato12) hora.Add(new Parte(AmPm(t), 16, NORMAL, TENUE));
        Linea(g, o.Fecha ? 184 : 214, 60, hora.ToArray());
        if (o.Fecha) Linea(g, 324, 60, new Parte(FechaCorta(t), 20, NORMAL, TENUE));
    }

    static void TresAnillos(Graphics g, Opciones o, DateTime t, Color[] c)
    {
        Disco(g, 140, 140, 130, FONDO);
        float seg = Segundo(t), min = t.Minute + seg / 60f, hora = t.Hour % 12 + min / 60f;
        ArcoProgreso(g, 140, 140, 118, 14, hora / 12f, c[2], false);
        ArcoProgreso(g, 140, 140, 98, 14, min / 60f, c[1], false);
        if (o.Segundos) ArcoProgreso(g, 140, 140, 78, 14, seg / 60f, c[0], false);

        List<Parte> abajo = new List<Parte>();
        if (o.Segundos) abajo.Add(new Parte(t.ToString("ss", ES), 18, NORMAL, c[0]));
        if (o.Formato12) abajo.Add(new Parte(AmPm(t), 14, NORMAL, TENUE));
        bool f = o.Fecha, s = abajo.Count > 0;
        float y = f && s ? 138 : f ? 146 : s ? 132 : 140;

        if (f) Linea(g, 140, y - 34, new Parte(FechaCorta(t), 15, NORMAL, TENUE));
        Linea(g, 140, y, new Parte(Hora(o, t), 40, SEMI, BLANCO));
        if (s) Linea(g, 140, y + 32, abajo.ToArray());
    }

    static void Tarjetas(Graphics g, Opciones o, DateTime t, Color[] c)
    {
        Tarjeta(g, 22, t.ToString(o.Formato12 ? "hh" : "HH", ES), c[0]);
        Tarjeta(g, 208, t.ToString("mm", ES), c[0]);

        Color punto = t.Millisecond < 500 ? c[0] : Color.FromArgb(80, 255, 255, 255);
        using (SolidBrush b = new SolidBrush(punto))
        {
            g.FillEllipse(b, 185, 73, 10, 10);
            g.FillEllipse(b, 185, 109, 10, 10);
        }

        if (o.Segundos)
        {
            using (SolidBrush pista = new SolidBrush(Color.FromArgb(50, 255, 255, 255))) g.FillRectangle(pista, 140, 178, 100, 4);
            using (SolidBrush b = new SolidBrush(c[0])) g.FillRectangle(b, 140, 178, 100 * Segundo(t) / 60f, 4);
        }

        List<Parte> abajo = new List<Parte>();
        if (o.Fecha) abajo.Add(new Parte(FechaLarga(t), 15, NORMAL, TENUE));
        if (o.Formato12) abajo.Add(new Parte(AmPm(t), 15, SEMI, c[0]));
        if (abajo.Count > 0) Linea(g, 190, o.Segundos ? 198 : 188, abajo.ToArray());
    }

    // ---------- Cronómetro ----------

    // Píldora del cronómetro que va debajo del reloj (lienzo de CRONO_ANCHO x CRONO_ALTO a escala 1).
    public const int CRONO_ANCHO = 210, CRONO_ALTO = 46, CRONO_SEPARACION = 10;

    public static void DibujarCronometro(Graphics g, Opciones o, TimeSpan t, bool corriendo, DateTime ahora)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color[] c = Paleta.Colores(o, ahora);
        using (GraphicsPath gp = Redondeado(1, 1, CRONO_ANCHO - 2, CRONO_ALTO - 2, (CRONO_ALTO - 2) / 2f))
        {
            using (SolidBrush b = new SolidBrush(FONDO)) g.FillPath(b, gp);
            using (Pen p = new Pen(Color.FromArgb(130, c[0]), 1.5f)) g.DrawPath(p, gp);
        }

        // Icono: esfera de cronómetro con su botón y una aguja que gira con los segundos
        float cx = 26, cy = CRONO_ALTO / 2f + 2, r = 11;
        using (Pen p = new Pen(corriendo ? c[0] : TENUE, 2.4f))
        {
            p.StartCap = p.EndCap = LineCap.Round;
            g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
            g.DrawLine(p, cx, cy - r - 1, cx, cy - r - 4);
            g.DrawLine(p, cx - 3, cy - r - 5, cx + 3, cy - r - 5);
            double a = (t.TotalSeconds % 60) / 60 * 2 * Math.PI;
            g.DrawLine(p, cx, cy, cx + (r - 4) * (float)Math.Sin(a), cy - (r - 4) * (float)Math.Cos(a));
        }

        string s = t.TotalHours >= 1
            ? (int)t.TotalHours + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00")
            : t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
        Linea(g, 120, CRONO_ALTO / 2f, new Parte(s, 24, SEMI, corriendo ? BLANCO : TENUE),
                                      new Parte("." + t.Milliseconds / 100, 16, SEMI, c[0]));

        if (!corriendo) // en pausa: dos barras a la derecha
            using (SolidBrush b = new SolidBrush(TENUE))
            {
                g.FillRectangle(b, CRONO_ANCHO - 30, CRONO_ALTO / 2f - 7, 4, 14);
                g.FillRectangle(b, CRONO_ANCHO - 23, CRONO_ALTO / 2f - 7, 4, 14);
            }
    }

    // ---------- Alarma ----------

    // Despertador que se sacude cuando suena la alarma (lienzo de DESPERTADOR_ANCHO x DESPERTADOR_ALTO).
    // angulo: inclinación actual en grados · pulso: 0..1 para las ondas de sonido.
    public const int DESPERTADOR_ANCHO = 280, DESPERTADOR_ALTO = 290;

    // titulo: el del evento del calendario (vacío en las alarmas normales); se escribe bajo la hora.
    public static void DibujarDespertador(Graphics g, Opciones o, DateTime ahora, float angulo, float pulso, string hora, string titulo)
    {
        g.TranslateTransform(20, 0); // el lienzo es más ancho que el dibujo, para que quepan títulos largos
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Color ac = Paleta.Colores(o, ahora)[0];
        const float cx = 120, cy = 116;

        // Ondas de sonido a los lados (no giran con el despertador)
        for (int i = 0; i < 2; i++)
        {
            float rr = 90 + i * 15 + pulso * 8;
            int alfa = (int)(200 * (1 - pulso * 0.7f) / (i + 1));
            using (Pen p = new Pen(Color.FromArgb(Math.Max(0, alfa), ac), 5))
            {
                p.StartCap = p.EndCap = LineCap.Round;
                g.DrawArc(p, cx - rr, cy - rr, 2 * rr, 2 * rr, 160, 40);
                g.DrawArc(p, cx - rr, cy - rr, 2 * rr, 2 * rr, -20, 40);
            }
        }

        GraphicsState estado = g.Save();
        g.TranslateTransform(cx, cy);
        g.RotateTransform(angulo);
        g.TranslateTransform(-cx, -cy);

        using (Pen sombra = new Pen(Color.FromArgb(120, 0, 0, 0), 12))
        using (Pen patas = new Pen(TENUE, 8))
        {
            sombra.StartCap = sombra.EndCap = patas.StartCap = patas.EndCap = LineCap.Round;
            g.DrawLine(sombra, cx - 40, cy + 52, cx - 54, cy + 72);
            g.DrawLine(sombra, cx + 40, cy + 52, cx + 54, cy + 72);
            g.DrawLine(patas, cx - 40, cy + 52, cx - 54, cy + 72);
            g.DrawLine(patas, cx + 40, cy + 52, cx + 54, cy + 72);
        }

        // Martillo y campanas
        using (Pen p = new Pen(TENUE, 5)) { p.StartCap = p.EndCap = LineCap.Round; g.DrawLine(p, cx, cy - 64, cx, cy - 80); }
        using (SolidBrush b = new SolidBrush(TENUE)) g.FillEllipse(b, cx - 7, cy - 92, 14, 14);
        using (SolidBrush b = new SolidBrush(ac))
        using (Pen borde = new Pen(Color.FromArgb(120, 0, 0, 0), 3))
        {
            g.FillPie(b, cx - 76, cy - 90, 56, 56, 135, 180);
            g.DrawPie(borde, cx - 76, cy - 90, 56, 56, 135, 180);
            g.FillPie(b, cx + 20, cy - 90, 56, 56, 225, 180);
            g.DrawPie(borde, cx + 20, cy - 90, 56, 56, 225, 180);
        }

        // Cuerpo y esfera
        using (SolidBrush b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillEllipse(b, cx - 70, cy - 66, 140, 140);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(246, 246, 250))) g.FillEllipse(b, cx - 64, cy - 64, 128, 128);
        using (Pen p = new Pen(ac, 10)) g.DrawEllipse(p, cx - 64, cy - 64, 128, 128);
        using (Pen p = new Pen(Color.FromArgb(90, 90, 100), 3))
            for (int i = 0; i < 12; i++)
            {
                double a = i * Math.PI / 6;
                float s = (float)Math.Sin(a), co = (float)Math.Cos(a);
                g.DrawLine(p, cx + 44 * s, cy - 44 * co, cx + 51 * s, cy - 51 * co);
            }

        // Agujas con la hora actual
        float min = ahora.Minute + ahora.Second / 60f, hr = ahora.Hour % 12 + min / 60f;
        using (Pen ph = new Pen(Color.FromArgb(40, 44, 56), 7))
        using (Pen pm = new Pen(Color.FromArgb(40, 44, 56), 4.5f))
        {
            ph.StartCap = ph.EndCap = pm.StartCap = pm.EndCap = LineCap.Round;
            double ah = hr * 30 * Math.PI / 180, am = min * 6 * Math.PI / 180;
            g.DrawLine(ph, cx, cy, cx + 28 * (float)Math.Sin(ah), cy - 28 * (float)Math.Cos(ah));
            g.DrawLine(pm, cx, cy, cx + 42 * (float)Math.Sin(am), cy - 42 * (float)Math.Cos(am));
        }
        using (SolidBrush b = new SolidBrush(ac)) g.FillEllipse(b, cx - 6, cy - 6, 12, 12);
        g.Restore(estado);

        Linea(g, 120, 220, new Parte(hora, 30, SEMI, BLANCO));
        if (!string.IsNullOrEmpty(titulo))
        {
            string t = titulo.Length > 30 ? titulo.Substring(0, 29).TrimEnd() + "…" : titulo;
            Linea(g, 120, 250, new Parte(t, 15, SEMI, ac));
            Linea(g, 120, 276, new Parte("Clic para detener", 12, NORMAL, TENUE));
        }
        else Linea(g, 120, 252, new Parte("Clic para detener", 13, NORMAL, TENUE));
    }

    // ---------- Piezas comunes ----------

    static void Tarjeta(Graphics g, float x, string texto, Color acento)
    {
        using (GraphicsPath gp = Redondeado(x, 20, 150, 150, 14))
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(215, 20, 22, 30))) g.FillPath(b, gp);
            using (Pen p = new Pen(Color.FromArgb(110, acento), 1.5f)) g.DrawPath(p, gp);
        }
        Linea(g, x + 75, 95, new Parte(texto, 96, SEMI, BLANCO));
        using (Pen corte = new Pen(Color.FromArgb(170, 10, 10, 14), 3)) g.DrawLine(corte, x, 95, x + 150, 95);
    }

    static void Disco(Graphics g, float cx, float cy, float r, Color color)
    {
        using (SolidBrush b = new SolidBrush(color)) g.FillEllipse(b, cx - r, cy - r, r * 2, r * 2);
    }

    static void Marcas(Graphics g, float cx, float cy, float rHora, float rMinuto, float rExterior)
    {
        using (Pen ph = new Pen(Color.FromArgb(170, 255, 255, 255), 2.5f))
        using (Pen pm = new Pen(Color.FromArgb(70, 255, 255, 255), 1.2f))
        {
            for (int i = 0; i < 60; i++)
            {
                double a = i * Math.PI / 30;
                bool hora = i % 5 == 0;
                float r1 = hora ? rHora : rMinuto;
                float s = (float)Math.Sin(a), co = (float)Math.Cos(a);
                g.DrawLine(hora ? ph : pm, cx + r1 * s, cy - r1 * co, cx + rExterior * s, cy - rExterior * co);
            }
        }
    }

    static void ArcoProgreso(Graphics g, float cx, float cy, float r, float grosor, float fraccion, Color color, bool brillo)
    {
        RectangleF caja = new RectangleF(cx - r, cy - r, r * 2, r * 2);
        using (Pen pista = new Pen(Color.FromArgb(45, 255, 255, 255), grosor)) g.DrawEllipse(pista, caja);

        float barrido = Math.Max(0.5f, Math.Min(359.9f, fraccion * 360f));
        using (Pen arco = new Pen(color, grosor))
        {
            arco.StartCap = LineCap.Round;
            arco.EndCap = LineCap.Round;
            g.DrawArc(arco, caja, -90, barrido);
        }
        if (!brillo) return;

        double ap = (barrido - 90) * Math.PI / 180;
        float px = cx + r * (float)Math.Cos(ap), py = cy + r * (float)Math.Sin(ap);
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddEllipse(px - 14, py - 14, 28, 28);
            using (PathGradientBrush pb = new PathGradientBrush(gp))
            {
                pb.CenterColor = Color.FromArgb(230, 255, 255, 255);
                pb.SurroundColors = new Color[] { Color.FromArgb(0, color) };
                g.FillPath(pb, gp);
            }
        }
    }

    static void Aguja(Graphics g, float grados, float largo, float grosor, Color color)
    {
        double a = grados * Math.PI / 180;
        using (Pen sombra = new Pen(Color.FromArgb(110, 0, 0, 0), grosor + 3))
        using (Pen p = new Pen(color, grosor))
        {
            sombra.StartCap = sombra.EndCap = p.StartCap = p.EndCap = LineCap.Round;
            float x = 140 + largo * (float)Math.Sin(a), y = 140 - largo * (float)Math.Cos(a);
            float xc = 140 - 14 * (float)Math.Sin(a), yc = 140 + 14 * (float)Math.Cos(a); // pequeña cola
            g.DrawLine(sombra, xc, yc, x, y);
            g.DrawLine(p, xc, yc, x, y);
        }
    }

    public static GraphicsPath Redondeado(float x, float y, float w, float h, float r)
    {
        GraphicsPath gp = new GraphicsPath();
        float d = r * 2;
        gp.AddArc(x, y, d, d, 180, 90);
        gp.AddArc(x + w - d, y, d, d, 270, 90);
        gp.AddArc(x + w - d, y + h - d, d, d, 0, 90);
        gp.AddArc(x, y + h - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }

    // Escribe una línea de texto con varias partes (distinto tamaño o color) alineadas por la base,
    // centrada en cx; cy es el centro vertical de las cifras de la parte más grande.
    // Cada parte lleva un contorno oscuro para leerse sobre fondos claros.
    public static void Linea(Graphics g, float cx, float cy, params Parte[] partes)
    {
        float maxTam = 0;
        foreach (Parte p in partes) maxTam = Math.Max(maxTam, p.Tam);
        float baseY = cy + 0.35f * maxTam;

        GraphicsPath[] rutas = new GraphicsPath[partes.Length];
        RectangleF[] cajas = new RectangleF[partes.Length];
        float total = 0;
        for (int i = 0; i < partes.Length; i++)
        {
            rutas[i] = new GraphicsPath();
            rutas[i].AddString(partes[i].T, Familia(partes[i].Peso), 0, partes[i].Tam, PointF.Empty, StringFormat.GenericTypographic);
            cajas[i] = rutas[i].GetBounds();
            total += cajas[i].Width + Hueco(partes, i);
        }

        float x = cx - total / 2;
        for (int i = 0; i < partes.Length; i++)
        {
            Parte p = partes[i];
            x += Hueco(partes, i);
            FontFamily fam = Familia(p.Peso);
            float ascenso = p.Tam * fam.GetCellAscent(FontStyle.Regular) / fam.GetEmHeight(FontStyle.Regular);
            using (Matrix m = new Matrix())
            {
                m.Translate(x - cajas[i].X, baseY - ascenso);
                rutas[i].Transform(m);
            }
            float grosor = Math.Max(1.5f, p.Tam * (p.Peso == FINA ? 0.03f : 0.06f));
            using (Pen borde = new Pen(Color.FromArgb(140, 0, 0, 0), grosor))
            {
                borde.LineJoin = LineJoin.Round;
                g.DrawPath(borde, rutas[i]);
            }
            using (SolidBrush b = new SolidBrush(p.C)) g.FillPath(b, rutas[i]);
            x += cajas[i].Width;
            rutas[i].Dispose();
        }
    }

    // Alto de las cifras como fracción del tamaño de letra (Segoe UI): sirve para alinear por arriba.
    const float ALTO_CIFRAS = 0.7f;

    static float AnchoTexto(Parte p)
    {
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddString(p.T, Familia(p.Peso), 0, p.Tam, PointF.Empty, StringFormat.GenericTypographic);
            return gp.GetBounds().Width;
        }
    }

    // Escribe una parte con su borde izquierdo en x y su línea base en baseY, con el mismo contorno que Linea.
    static void TextoEn(Graphics g, Parte p, float x, float baseY)
    {
        FontFamily fam = Familia(p.Peso);
        using (GraphicsPath gp = new GraphicsPath())
        {
            gp.AddString(p.T, fam, 0, p.Tam, PointF.Empty, StringFormat.GenericTypographic);
            RectangleF b = gp.GetBounds();
            float ascenso = p.Tam * fam.GetCellAscent(FontStyle.Regular) / fam.GetEmHeight(FontStyle.Regular);
            using (Matrix m = new Matrix())
            {
                m.Translate(x - b.X, baseY - ascenso);
                gp.Transform(m);
            }
            using (Pen borde = new Pen(Color.FromArgb(140, 0, 0, 0), Math.Max(1.5f, p.Tam * (p.Peso == FINA ? 0.03f : 0.06f))))
            {
                borde.LineJoin = LineJoin.Round;
                g.DrawPath(borde, gp);
            }
            using (SolidBrush br = new SolidBrush(p.C)) g.FillPath(br, gp);
        }
    }

    // Espacio antes de la parte i (las que empiezan con ":" van pegadas a la anterior).
    static float Hueco(Parte[] partes, int i)
    {
        if (i == 0 || partes[i].T.StartsWith(":") || partes[i].T.StartsWith(".")) return i == 0 ? 0 : partes[i].Tam * 0.08f;
        return Math.Max(partes[i].Tam, partes[i - 1].Tam * 0.5f) * 0.3f;
    }

    static FontFamily Familia(int peso)
    {
        FontFamily f;
        if (familias.TryGetValue(peso, out f)) return f;
        string nombre = peso == SEMI ? "Segoe UI Semibold" : peso == FINA ? "Segoe UI Light" : "Segoe UI";
        try { f = new FontFamily(nombre); } catch { f = FontFamily.GenericSansSerif; }
        familias[peso] = f;
        return f;
    }

    static float Segundo(DateTime t) { return t.Second + t.Millisecond / 1000f; }
    static string Hora(Opciones o, DateTime t) { return t.ToString(o.Formato12 ? "h:mm" : "HH:mm", ES); }
    static string AmPm(DateTime t) { return t.Hour < 12 ? "a. m." : "p. m."; }

    static string FechaLarga(DateTime t)
    {
        string s = t.ToString("dddd d 'de' MMMM", ES);
        return char.ToUpper(s[0]) + s.Substring(1);
    }

    static string FechaCorta(DateTime t)
    {
        string dia = t.ToString("dddd", ES); // "viernes" → "Vie" (el formato ddd da "vi.")
        return char.ToUpper(dia[0]) + dia.Substring(1, 2) + " " + t.Day;
    }
}

// ---------- Ventana del overlay ----------

class Overlay : Form
{
    readonly Opciones op;
    readonly List<Bitmap> fotogramas = new List<Bitmap>();
    readonly List<int> retardos = new List<int>();
    IntPtr[] hbitmaps = new IntPtr[0];
    Size tam;
    int actual;
    bool mostrando;
    readonly System.Windows.Forms.Timer temporizador = new System.Windows.Forms.Timer();
    readonly NotifyIcon bandeja = new NotifyIcon();
    readonly List<string> atajosFallidos = new List<string>();
    string atajosRegistrados; // firma de los atajos registrados, para no repetir el registro
    Configuracion config;
    readonly List<Capa> capas = new List<Capa>();

    public Overlay(Opciones opciones)
    {
        op = opciones;
        Text = Program.TITULO;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;

        if (!op.Reloj) CargarImagen(op.Ruta);
        temporizador.Interval = 100;
        temporizador.Tick += delegate { Avanzar(); };

        bandeja.Icon = Iconos.Pequeno;
        bandeja.Text = Program.NOMBRE;
        ContextMenuStrip menu = new ContextMenuStrip();
        ToolStripMenuItem mDetener = new ToolStripMenuItem("Detener la alarma", null, delegate { DetenerAlarma(); });
        ToolStripMenuItem mAlternar = new ToolStripMenuItem("Mostrar / ocultar", null, delegate { Ejecutar(Acciones.ALTERNAR); });
        ToolStripMenuItem mConfig = new ToolStripMenuItem("Configuración…", null, delegate { AbrirConfiguracion(); });
        ToolStripMenuItem mPosicion = new ToolStripMenuItem("Cambiar posición", null, delegate { CambiarPosicion(); });
        ToolStripMenuItem mCrono = new ToolStripMenuItem("Iniciar cronómetro", null, delegate { CronometroAlternar(); });
        ToolStripMenuItem mReiniciar = new ToolStripMenuItem("Reiniciar cronómetro", null, delegate { CronometroReiniciar(); });
        ToolStripMenuItem mSalir = new ToolStripMenuItem("Salir", null, delegate { Close(); });
        ToolStripSeparator sepAlarma = new ToolStripSeparator();
        menu.Items.AddRange(new ToolStripItem[] { mDetener, sepAlarma, mAlternar, mConfig, mPosicion, new ToolStripSeparator(),
                                                  mCrono, mReiniciar, new ToolStripSeparator(), mSalir });
        menu.Opening += delegate
        {
            // Muestra al lado de cada opción el atajo configurado (si tiene) y el estado actual.
            mDetener.Visible = sepAlarma.Visible = AlarmaSonando;
            mAlternar.ShortcutKeyDisplayString = op.Atajos[Acciones.ALTERNAR].Mostrar();
            mConfig.ShortcutKeyDisplayString = op.Atajos[Acciones.CONFIG].Mostrar();
            mPosicion.ShortcutKeyDisplayString = op.Atajos[Acciones.POSICION].Mostrar();
            mCrono.Text = crono.IsRunning ? "Pausar cronómetro" : CronometroVisible ? "Continuar cronómetro" : "Iniciar cronómetro";
            mCrono.ShortcutKeyDisplayString = op.Atajos[Acciones.CRONO].Mostrar();
            mReiniciar.Enabled = CronometroVisible;
            mReiniciar.ShortcutKeyDisplayString = op.Atajos[Acciones.CRONO_REINICIAR].Mostrar();
            mSalir.ShortcutKeyDisplayString = op.Atajos[Acciones.SALIR].Mostrar();
        };
        bandeja.ContextMenuStrip = menu;
        bandeja.DoubleClick += delegate { if (AlarmaSonando) DetenerAlarma(); else AbrirConfiguracion(); };
        bandeja.Visible = true;

        CargarEventos(); // eventos de la última sincronización del calendario

        // Las alarmas se vigilan siempre, aunque el reloj esté oculto.
        vigilante.Interval = 500;
        vigilante.Tick += delegate { VigilarAlarmas(); };
        vigilante.Start();
        animacionAlarma.Interval = 33;
        animacionAlarma.Tick += delegate { AnimarAlarma(); };

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += PantallasCambiaron; // monitor conectado/desconectado
    }

    // ---------- Cronómetro ----------

    readonly System.Diagnostics.Stopwatch crono = new System.Diagnostics.Stopwatch();

    public bool CronometroCorriendo { get { return crono.IsRunning; } }
    public TimeSpan TiempoCronometro { get { return crono.Elapsed; } }
    // Se ve debajo del reloj mientras corre o está en pausa con tiempo; al reiniciarlo desaparece.
    public bool CronometroVisible { get { return op.Reloj && (crono.IsRunning || crono.ElapsedTicks > 0); } }

    public void CronometroAlternar()
    {
        if (crono.IsRunning) crono.Stop();
        else
        {
            crono.Start();
            if (!mostrando) Alternar(); // al iniciarlo, el reloj se muestra para verlo
        }
        Pintar();
    }

    public void CronometroReiniciar()
    {
        crono.Reset();
        Pintar();
    }

    // ---------- Alarmas ----------

    readonly System.Windows.Forms.Timer vigilante = new System.Windows.Forms.Timer();
    readonly System.Windows.Forms.Timer animacionAlarma = new System.Windows.Forms.Timer();
    readonly List<VentanaAlarma> ventanasAlarma = new List<VentanaAlarma>();
    readonly HashSet<string> alarmasDisparadas = new HashSet<string>();
    System.Media.SoundPlayer sonido;
    DateTime inicioAlarma;
    string horaAlarma = "", tituloAlarma = "";

    public bool AlarmaSonando { get { return ventanasAlarma.Count > 0; } }

    // ---------- Calendarios sincronizados ----------

    // Eventos de la última sincronización (8 días desde ese momento). Se guardan en eventos.cache para que los
    // avisos sigan funcionando tras reiniciar la PC; solo se descargan de nuevo al pulsar "Sincronizar calendario".
    public List<EventoCalendario> Eventos = new List<EventoCalendario>();
    public DateTime? UltimaSincronizacion { get; private set; }
    public string ErrorCalendario { get; private set; } // null si la última sincronización fue bien
    public bool Sincronizando { get { return sincronizando; } }
    public event EventHandler CalendarioActualizado;
    bool sincronizando;

    static string ArchivoEventos { get { return Path.Combine(Opciones.Carpeta, "eventos.cache"); } }

    // Formato: primera línea "sincronizado=yyyyMMddHHmm"; luego uid, inicio, fin, todo el día, calendario y título
    // separados por tabuladores.
    void GuardarEventos()
    {
        try
        {
            List<string> l = new List<string>();
            if (UltimaSincronizacion.HasValue) l.Add("sincronizado=" + UltimaSincronizacion.Value.ToString("yyyyMMddHHmm"));
            foreach (EventoCalendario e in Eventos)
                l.Add(string.Join("\t", new string[] { e.Uid.Replace("\t", " "), e.Inicio.ToString("yyyyMMddHHmm"), e.Fin.ToString("yyyyMMddHHmm"),
                                                       e.TodoElDia ? "1" : "0", e.Fuente.Replace("\t", " "), e.Titulo.Replace("\t", " ") }));
            Directory.CreateDirectory(Opciones.Carpeta);
            File.WriteAllLines(ArchivoEventos, l.ToArray());
        }
        catch { }
    }

    void CargarEventos()
    {
        try
        {
            if (!File.Exists(ArchivoEventos)) return;
            System.Globalization.CultureInfo inv = System.Globalization.CultureInfo.InvariantCulture;
            List<EventoCalendario> l = new List<EventoCalendario>();
            foreach (string linea in File.ReadAllLines(ArchivoEventos))
            {
                if (linea.StartsWith("sincronizado="))
                {
                    UltimaSincronizacion = DateTime.ParseExact(linea.Substring(13), "yyyyMMddHHmm", inv);
                    continue;
                }
                string[] p = linea.Split('\t');
                if (p.Length < 6) continue;
                EventoCalendario e = new EventoCalendario();
                e.Uid = p[0];
                e.Inicio = DateTime.ParseExact(p[1], "yyyyMMddHHmm", inv);
                e.Fin = DateTime.ParseExact(p[2], "yyyyMMddHHmm", inv);
                e.TodoElDia = p[3] == "1";
                e.Fuente = p[4];
                e.Titulo = p[5];
                l.Add(e);
            }
            Eventos = l;
        }
        catch { }
    }

    // Quita los eventos de calendarios que ya no están en la configuración (al eliminar una sincronización),
    // sin volver a descargar los demás.
    public void PodarEventos()
    {
        List<string> nombres = new List<string>();
        foreach (ConexionCalendario c in op.Calendarios) nombres.Add(c.Nombre);
        int antes = Eventos.Count;
        Eventos = Eventos.FindAll(delegate(EventoCalendario e) { return nombres.Contains(e.Fuente); });
        if (Eventos.Count == antes && op.Calendarios.Count > 0) return;
        if (op.Calendarios.Count == 0) { UltimaSincronizacion = null; ErrorCalendario = null; }
        GuardarEventos();
        if (CalendarioActualizado != null) CalendarioActualizado(this, EventArgs.Empty);
    }

    // Descarga los calendarios en segundo plano (solo cuando se pulsa "Sincronizar calendario")
    // y, al terminar, avisa con CalendarioActualizado.
    public void SincronizarCalendarios()
    {
        if (sincronizando || !IsHandleCreated) return;
        List<ConexionCalendario> conexiones = new List<ConexionCalendario>();
        foreach (ConexionCalendario c in op.Calendarios) conexiones.Add(c.Copia());
        if (conexiones.Count == 0)
        {
            Eventos = new List<EventoCalendario>();
            ErrorCalendario = null;
            UltimaSincronizacion = null;
            GuardarEventos();
            if (CalendarioActualizado != null) CalendarioActualizado(this, EventArgs.Empty);
            return;
        }
        sincronizando = true;
        if (CalendarioActualizado != null) CalendarioActualizado(this, EventArgs.Empty);
        ThreadPool.QueueUserWorkItem(delegate
        {
            List<EventoCalendario> todos = new List<EventoCalendario>();
            List<string> errores = new List<string>();
            DateTime desde = DateTime.Today, hasta = DateTime.Today.AddDays(8);
            foreach (ConexionCalendario c in conexiones)
            {
                try { todos.AddRange(ICal.Descargar(c.Url, c.Nombre, desde, hasta)); }
                catch (Exception ex) { errores.Add(c.Nombre + ": " + ex.Message); }
            }
            todos.Sort(delegate(EventoCalendario a, EventoCalendario b) { return a.Inicio.CompareTo(b.Inicio); });
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    Eventos = todos;
                    ErrorCalendario = errores.Count > 0 ? string.Join("\n", errores.ToArray()) : null;
                    UltimaSincronizacion = DateTime.Now;
                    sincronizando = false;
                    GuardarEventos();
                    if (CalendarioActualizado != null) CalendarioActualizado(this, EventArgs.Empty);
                }));
            }
            catch { } // la app se cerró mientras se descargaba
        });
    }

    void VigilarAlarmas()
    {
        DateTime ahora = DateTime.Now;
        if (AlarmaSonando && (ahora - inicioAlarma).TotalMinutes >= 2) DetenerAlarma(); // se apaga sola a los 2 minutos

        // Aviso de los eventos del calendario, unos minutos antes de que empiecen.
        // (Los calendarios solo se descargan al pulsar "Sincronizar calendario"; aquí se usan los ya guardados.)
        if (op.AvisoCalendario > 0)
            foreach (EventoCalendario e in Eventos)
            {
                if (e.TodoElDia || op.EventosSilenciados.Contains(e.Clave)) continue;
                DateTime aviso = e.Inicio.AddMinutes(-op.AvisoCalendario);
                if (aviso.Date != ahora.Date || aviso.Hour != ahora.Hour || aviso.Minute != ahora.Minute) continue;
                if (alarmasDisparadas.Add("calendario|" + e.Clave)) SonarAlarma(e.Inicio.ToString("HH:mm"), e.Titulo);
            }

        foreach (Alarma a in op.Alarmas)
        {
            if (!a.Activa || a.Hora != ahora.Hour || a.Minuto != ahora.Minute || !a.TocaEl(ahora)) continue;
            if (!alarmasDisparadas.Add(ahora.ToString("yyyyMMddHHmm") + "|" + a.HoraTexto())) continue; // ya sonó este minuto
            if (a.UnaSolaVez)
            {
                a.Activa = false; // las de "Solo hoy" se desactivan al sonar
                if (config != null && !config.IsDisposed) config.RefrescarAlarmas();
                else try { op.Guardar(); } catch { }
            }
            SonarAlarma(a.HoraTexto());
        }
    }

    // Muestra el despertador animado en cada pantalla del reloj y, si está activado, suena.
    public void SonarAlarma(string hora)
    {
        SonarAlarma(hora, "");
    }

    // titulo: el del evento del calendario (o vacío para las alarmas normales).
    public void SonarAlarma(string hora, string titulo)
    {
        horaAlarma = hora;
        tituloAlarma = titulo ?? "";
        inicioAlarma = DateTime.Now;
        if (!AlarmaSonando)
        {
            foreach (Screen s in PantallasDestino())
            {
                VentanaAlarma v = new VentanaAlarma();
                v.Pantalla = s;
                v.Pulsada += delegate { DetenerAlarma(); };
                ventanasAlarma.Add(v);
            }
            AnimarAlarma();
            foreach (VentanaAlarma v in ventanasAlarma) v.Mostrar();
            animacionAlarma.Start();
        }
        if (op.SonidoAlarma)
        {
            try
            {
                if (sonido == null) sonido = new System.Media.SoundPlayer(Sonidos.Alarma());
                sonido.PlayLooping();
            }
            catch { } // sin dispositivo de audio: solo se ve el icono
        }
    }

    public void DetenerAlarma()
    {
        animacionAlarma.Stop();
        foreach (VentanaAlarma v in ventanasAlarma) v.Close();
        ventanasAlarma.Clear();
        if (sonido != null) sonido.Stop();
    }

    void AnimarAlarma()
    {
        double t = (DateTime.Now - inicioAlarma).TotalSeconds;
        // Se sacude 1,1 s y descansa 0,5 s, como un despertador de campanas.
        double ciclo = t % 1.6;
        float angulo = ciclo < 1.1 ? (float)(14 * Math.Sin(ciclo * 2 * Math.PI * 7) * Math.Sin(ciclo / 1.1 * Math.PI)) : 0;
        float pulso = (float)((t * 1.6) % 1);

        Size s = new Size(Reloj.DESPERTADOR_ANCHO, Reloj.DESPERTADOR_ALTO);
        IntPtr h;
        using (Bitmap bmp = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp)) Reloj.DibujarDespertador(g, op, DateTime.Now, angulo, pulso, horaAlarma, tituloAlarma);
            h = bmp.GetHbitmap(Color.FromArgb(0));
        }
        try
        {
            foreach (VentanaAlarma v in ventanasAlarma)
            {
                Rectangle a = v.Pantalla.WorkingArea;
                CopiarACapa(v.Handle, h, new Point(a.Left + (a.Width - s.Width) / 2, a.Top + (a.Height - s.Height) / 2), s, 255);
            }
        }
        finally { Win32.DeleteObject(h); }
    }

    // Esta ventana nunca se ve: solo recibe los atajos y los comandos.
    // El reloj se dibuja en una "Capa" transparente por cada pantalla elegida.
    protected override void SetVisibleCore(bool value)
    {
        if (!IsHandleCreated)
        {
            CreateHandle();
            mostrando = !op.Oculto;
            ActualizarCapas();
            if (mostrando && Animado) temporizador.Start();
        }
        base.SetVisibleCore(false);
    }

    // Pantallas donde debe verse el reloj según la configuración.
    Screen[] PantallasDestino()
    {
        Screen[] todas = Screen.AllScreens;
        if (op.Pantalla == "*") return todas;
        foreach (Screen s in todas) if (s.DeviceName == op.Pantalla) return new Screen[] { s };
        return new Screen[] { Screen.PrimaryScreen }; // si el monitor elegido no está conectado
    }

    // Crea o cierra capas para que haya una por pantalla de destino.
    void ActualizarCapas()
    {
        Screen[] destino = PantallasDestino();
        while (capas.Count > destino.Length)
        {
            capas[capas.Count - 1].Close();
            capas.RemoveAt(capas.Count - 1);
        }
        while (capas.Count < destino.Length) capas.Add(new Capa());
        for (int i = 0; i < destino.Length; i++) capas[i].Pantalla = destino[i];

        Pintar();
        foreach (Capa c in capas)
        {
            if (mostrando && !c.Visible) c.Mostrar();
            else if (!mostrando && c.Visible) c.Hide();
        }
    }

    void PantallasCambiaron(object sender, EventArgs e)
    {
        if (IsHandleCreated) BeginInvoke(new MethodInvoker(ActualizarCapas));
    }

    bool Animado { get { return op.Reloj || fotogramas.Count > 1; } }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegistrarAtajos();
        Reconstruir();

        if (atajosFallidos.Count > 0)
            bandeja.ShowBalloonTip(5000, Program.NOMBRE,
                "Atajos ocupados por otra aplicación: " + string.Join(", ", atajosFallidos.ToArray()), ToolTipIcon.Warning);
        else if (!op.AbrirConfig)
            bandeja.ShowBalloonTip(3000, Program.NOMBRE + " activo", Acciones.Resumen(op.Atajos), ToolTipIcon.Info);

        if (op.AbrirConfig) BeginInvoke(new MethodInvoker(AbrirConfiguracion));
    }

    // Registra los atajos configurados (el id de cada atajo es el número de acción + 1).
    // Devuelve los que no se pudieron registrar porque otra aplicación ya los usa.
    public List<string> RegistrarAtajos()
    {
        if (atajosPausados) return new List<string>();
        string firma = Acciones.Firma(op.Atajos);
        if (firma == atajosRegistrados) return new List<string>(atajosFallidos);

        QuitarAtajos();
        atajosFallidos.Clear();
        for (int i = 0; i < Acciones.Nombres.Length; i++)
        {
            Atajo a = op.Atajos[i];
            if (a.Vacio) continue;
            uint mods = a.Mods | (Acciones.Repite[i] ? 0 : Win32.MOD_NOREPEAT);
            if (!Win32.RegisterHotKey(Handle, i + 1, mods, (uint)a.Tecla)) atajosFallidos.Add(a.Mostrar());
        }
        atajosRegistrados = firma;
        return new List<string>(atajosFallidos);
    }

    // Mientras se eligen atajos nuevos hay que soltarlos, o Windows se "comería" la combinación pulsada.
    bool atajosPausados;
    public bool AtajosPausados
    {
        get { return atajosPausados; }
        set
        {
            atajosPausados = value;
            if (value) QuitarAtajos();
            else RegistrarAtajos();
        }
    }

    public void QuitarAtajos()
    {
        for (int i = 0; i < Acciones.Nombres.Length; i++) Win32.UnregisterHotKey(Handle, i + 1);
        atajosRegistrados = null;
    }

    void Ejecutar(int accion)
    {
        switch (accion)
        {
            case Acciones.ALTERNAR: if (AlarmaSonando) DetenerAlarma(); else Alternar(); break; // si suena la alarma, la apaga
            case Acciones.CONFIG: AbrirConfiguracion(); break;
            case Acciones.CRONO: CronometroAlternar(); break;
            case Acciones.CRONO_REINICIAR: CronometroReiniciar(); break;
            case Acciones.POSICION: CambiarPosicion(); break;
            case Acciones.GRANDE: op.PantallaCompleta = false; op.Escala = Math.Min(5f, op.Escala * 1.1f); Reconstruir(); GuardarAjuste(); break;
            case Acciones.PEQUENO: op.PantallaCompleta = false; op.Escala = Math.Max(0.2f, op.Escala / 1.1f); Reconstruir(); GuardarAjuste(); break;
            case Acciones.OPACO: op.Opacidad = Math.Min(1f, op.Opacidad + 0.1f); Pintar(); GuardarAjuste(); break;
            case Acciones.TRANSPARENTE: op.Opacidad = Math.Max(0.1f, op.Opacidad - 0.1f); Pintar(); GuardarAjuste(); break;
            case Acciones.SALIR: Close(); break;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Win32.WM_HOTKEY)
        {
            Ejecutar(m.WParam.ToInt32() - 1);
            return;
        }
        if (m.Msg == Program.WM_COMANDO)
        {
            switch (m.WParam.ToInt32())
            {
                case Program.CMD_ALTERNAR: Ejecutar(Acciones.ALTERNAR); break;
                case Program.CMD_MOSTRAR: if (!mostrando) Alternar(); break;
                case Program.CMD_OCULTAR: if (mostrando) Alternar(); break;
                case Program.CMD_CONFIG: AbrirConfiguracion(); break;
                case Program.CMD_SALIR: Close(); break;
            }
            return;
        }
        base.WndProc(ref m);
    }

    // Los cambios hechos con atajos en el modo reloj se recuerdan para la próxima vez.
    void GuardarAjuste()
    {
        if (!op.Reloj) return;
        try { op.Guardar(); } catch { }
    }

    public void AbrirConfiguracion()
    {
        if (!op.Reloj)
        {
            // La configuración es del reloj: si se estaba mostrando una imagen, pasamos al reloj.
            op.Reloj = true;
            temporizador.Interval = 100;
        }
        if (!mostrando) Alternar();
        if (config == null || config.IsDisposed)
        {
            config = new Configuracion(this, op);
            config.Show();
        }
        config.WindowState = FormWindowState.Normal;
        config.Activate();
    }

    // La ventana de configuración llama aquí cada vez que cambia algo.
    public void AplicarCambios()
    {
        Reconstruir();
        ActualizarCapas();
        RegistrarAtajos(); // solo vuelve a registrar si cambiaron
    }

    void Alternar()
    {
        mostrando = !mostrando;
        if (mostrando) Pintar();
        foreach (Capa c in capas)
        {
            if (mostrando) c.Mostrar();
            else c.Hide();
        }
        if (mostrando && Animado) temporizador.Start();
        else temporizador.Stop();
    }

    void CambiarPosicion()
    {
        int n = (op.PosV * 3 + op.PosH + 1) % 9; // recorre las 9 posiciones
        op.PosV = n / 3;
        op.PosH = n % 3;
        Pintar();
        GuardarAjuste();
    }

    // ---------- Imagen GIF / PNG ----------

    void CargarImagen(string ruta)
    {
        using (Image img = Image.FromFile(ruta))
        {
            int n = 1;
            FrameDimension dim = null;
            if (img.FrameDimensionsList.Length > 0)
            {
                dim = new FrameDimension(img.FrameDimensionsList[0]);
                n = img.GetFrameCount(dim);
            }

            byte[] delays = null;
            try { delays = img.GetPropertyItem(0x5100).Value; } catch { } // retardos del GIF (1/100 s)

            for (int i = 0; i < n; i++)
            {
                if (n > 1) img.SelectActiveFrame(dim, i);
                fotogramas.Add(new Bitmap(img)); // copia en 32 bits ARGB
                int d = (delays != null && delays.Length >= (i + 1) * 4) ? BitConverter.ToInt32(delays, i * 4) * 10 : 100;
                retardos.Add(d < 20 ? 100 : d); // los navegadores tratan retardos muy pequeños como 100 ms
            }
        }
    }

    // ---------- Dibujo ----------

    // Calcula el tamaño final y, para imágenes, pre-escala los fotogramas a mapas de bits GDI.
    void Reconstruir()
    {
        LiberarHbitmaps();
        if (op.Reloj)
        {
            Size b = Reloj.Base(op.Diseno);
            tam = new Size(Math.Max(20, (int)(b.Width * op.Escala)), Math.Max(20, (int)(b.Height * op.Escala)));
            Pintar();
            return;
        }
        if (fotogramas.Count == 0) return;

        tam = new Size(Math.Max(1, (int)(fotogramas[0].Width * op.Escala)),
                       Math.Max(1, (int)(fotogramas[0].Height * op.Escala)));
        hbitmaps = new IntPtr[fotogramas.Count];
        for (int i = 0; i < fotogramas.Count; i++)
        {
            using (Bitmap esc = new Bitmap(tam.Width, tam.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(esc))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.DrawImage(fotogramas[i], new Rectangle(0, 0, tam.Width, tam.Height));
                }
                hbitmaps[i] = esc.GetHbitmap(Color.FromArgb(0));
            }
        }
        if (actual >= hbitmaps.Length) actual = 0;
        Pintar();
    }

    void Avanzar()
    {
        if (op.Reloj) { Pintar(); return; }
        if (hbitmaps.Length < 2) return;
        actual = (actual + 1) % hbitmaps.Length;
        temporizador.Interval = retardos[actual];
        Pintar();
    }

    bool Completa { get { return op.Reloj && op.PantallaCompleta; } }

    // Tamaño del bloque (reloj y, si está a la vista, el cronómetro debajo) en una pantalla, y su escala k:
    // la elegida, o en pantalla completa lo más grande que quepa en el monitor sin deformarse.
    Size TamanoPara(Screen pantalla, out float k)
    {
        Size b = Reloj.Base(op.Diseno);
        bool cr = CronometroVisible;
        float ancho = Math.Max(b.Width, cr ? Reloj.CRONO_ANCHO : 0);
        float alto = b.Height + (cr ? Reloj.CRONO_SEPARACION + Reloj.CRONO_ALTO : 0);
        if (Completa)
        {
            Rectangle area = pantalla.Bounds;
            k = Math.Min(area.Width / ancho, area.Height / alto);
        }
        else k = op.Escala;
        return new Size(Math.Max(20, (int)(ancho * k)), Math.Max(20, (int)(alto * k)));
    }

    // Coloca el reloj según la posición vertical (arriba/centro/abajo) y horizontal (izquierda/centro/derecha).
    Point Ubicacion(Screen pantalla, Size s)
    {
        Rectangle area = Completa ? pantalla.Bounds : pantalla.WorkingArea;
        int margen = Completa ? 0 : 20;
        int x = op.PosH == 0 ? area.Left + margen
              : op.PosH == 1 ? area.Left + (area.Width - s.Width) / 2
              : area.Right - s.Width - margen;
        int y = op.PosV == 0 ? area.Top + margen
              : op.PosV == 1 ? area.Top + (area.Height - s.Height) / 2
              : area.Bottom - s.Height - margen;
        return new Point(x, y);
    }

    void Pintar()
    {
        if (!IsHandleCreated) return;
        if (!op.Reloj)
        {
            if (hbitmaps.Length > 0) foreach (Capa c in capas) Pintar(c, hbitmaps[actual], tam);
            return;
        }

        // Un dibujo por cada tamaño distinto (en pantalla completa los monitores pueden ser diferentes).
        DateTime ahora = DateTime.Now;
        Dictionary<Size, IntPtr> dibujos = new Dictionary<Size, IntPtr>();
        try
        {
            foreach (Capa c in capas)
            {
                if (c.Pantalla == null) continue;
                float k;
                Size s = TamanoPara(c.Pantalla, out k);
                IntPtr h;
                if (!dibujos.TryGetValue(s, out h))
                {
                    h = DibujarReloj(s, k, ahora);
                    dibujos[s] = h;
                }
                Pintar(c, h, s);
            }
        }
        finally
        {
            foreach (IntPtr h in dibujos.Values) Win32.DeleteObject(h);
        }
    }

    IntPtr DibujarReloj(Size s, float k, DateTime ahora)
    {
        using (Bitmap bmp = new Bitmap(s.Width, s.Height, PixelFormat.Format32bppArgb))
        {
            using (Graphics g = Graphics.FromImage(bmp))
            {
                Size b = Reloj.Base(op.Diseno);
                g.TranslateTransform((s.Width - b.Width * k) / 2, 0);
                g.ScaleTransform(k, k);
                Reloj.Dibujar(g, op, ahora);
                if (CronometroVisible)
                {
                    g.ResetTransform();
                    g.TranslateTransform((s.Width - Reloj.CRONO_ANCHO * k) / 2, (b.Height + Reloj.CRONO_SEPARACION) * k);
                    g.ScaleTransform(k, k);
                    Reloj.DibujarCronometro(g, op, crono.Elapsed, crono.IsRunning, ahora);
                }
            }
            return bmp.GetHbitmap(Color.FromArgb(0));
        }
    }

    // Copia un dibujo a la capa de una pantalla, en su posición.
    void Pintar(Capa c, IntPtr hbitmap, Size s)
    {
        if (c.Pantalla == null) return;
        CopiarACapa(c.Handle, hbitmap, Ubicacion(c.Pantalla, s), s, (byte)(op.Opacidad * 255));
    }

    // Pone un mapa de bits con transparencia en una ventana en capas.
    static void CopiarACapa(IntPtr ventana, IntPtr hbitmap, Point p, Size s, byte opacidad)
    {
        IntPtr pantalla = Win32.GetDC(IntPtr.Zero);
        IntPtr memoria = Win32.CreateCompatibleDC(pantalla);
        IntPtr anterior = Win32.SelectObject(memoria, hbitmap);
        try
        {
            Win32.POINT destino = new Win32.POINT(p.X, p.Y);
            Win32.POINT origen = new Win32.POINT(0, 0);
            Win32.SIZE tamano = new Win32.SIZE(s.Width, s.Height);
            Win32.BLENDFUNCTION mezcla = new Win32.BLENDFUNCTION();
            mezcla.BlendOp = Win32.AC_SRC_OVER;
            mezcla.SourceConstantAlpha = opacidad;
            mezcla.AlphaFormat = Win32.AC_SRC_ALPHA;
            Win32.UpdateLayeredWindow(ventana, pantalla, ref destino, ref tamano, memoria, ref origen, 0, ref mezcla, Win32.ULW_ALPHA);
        }
        finally
        {
            Win32.SelectObject(memoria, anterior);
            Win32.DeleteDC(memoria);
            Win32.ReleaseDC(IntPtr.Zero, pantalla);
        }
    }

    void LiberarHbitmaps()
    {
        foreach (IntPtr h in hbitmaps) if (h != IntPtr.Zero) Win32.DeleteObject(h);
        hbitmaps = new IntPtr[0];
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        temporizador.Stop();
        vigilante.Stop();
        DetenerAlarma();
        if (sonido != null) sonido.Dispose();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= PantallasCambiaron;
        if (config != null && !config.IsDisposed) config.Close();
        foreach (Capa c in capas) c.Close();
        capas.Clear();
        QuitarAtajos();
        bandeja.Visible = false;
        bandeja.Dispose();
        LiberarHbitmaps();
        foreach (Bitmap b in fotogramas) b.Dispose();
        base.OnFormClosed(e);
    }
}

// Ventana transparente donde se ve el reloj en una pantalla: en capas, deja pasar los clics,
// sin botón en la barra de tareas, nunca roba el foco y siempre está encima.
class Capa : Form
{
    public Screen Pantalla;

    public Capa()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        CreateHandle();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_TOOLWINDOW
                        | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    public void Mostrar()
    {
        Show();
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }
}

// Ventana del despertador cuando suena una alarma: como la Capa, pero recibe el clic para apagarla
// (solo donde se ve el dibujo) y sigue sin robar el foco.
class VentanaAlarma : Form
{
    public Screen Pantalla;
    public event EventHandler Pulsada;
    const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;

    public VentanaAlarma()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Cursor = Cursors.Hand;
        CreateHandle();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= Win32.WS_EX_LAYERED | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE | Win32.WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_MOUSEACTIVATE) { m.Result = (IntPtr)MA_NOACTIVATE; return; }
        base.WndProc(ref m);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (Pulsada != null) Pulsada(this, EventArgs.Empty);
    }

    public void Mostrar()
    {
        Show();
        Win32.SetWindowPos(Handle, Win32.HWND_TOPMOST, 0, 0, 0, 0,
            Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
    }
}

// Muestra un número grande en cada pantalla durante 2 segundos para saber cuál es cuál.
static class Identificador
{
    public static void Mostrar()
    {
        Screen[] todas = Screen.AllScreens;
        for (int i = 0; i < todas.Length; i++)
        {
            Form f = new Form();
            f.FormBorderStyle = FormBorderStyle.None;
            f.ShowInTaskbar = false;
            f.TopMost = true;
            f.StartPosition = FormStartPosition.Manual;
            f.BackColor = Color.FromArgb(20, 22, 30);
            f.Opacity = 0.9;
            f.Size = new Size(260, 200);
            Rectangle b = todas[i].Bounds;
            f.Location = new Point(b.Left + (b.Width - f.Width) / 2, b.Top + (b.Height - f.Height) / 2);

            Label n = new Label();
            n.Dock = DockStyle.Fill;
            n.TextAlign = ContentAlignment.MiddleCenter;
            n.ForeColor = Color.White;
            n.Font = new Font("Segoe UI Semibold", 72f);
            n.Text = (i + 1).ToString();
            Label d = new Label();
            d.Dock = DockStyle.Bottom;
            d.Height = 34;
            d.TextAlign = ContentAlignment.MiddleCenter;
            d.ForeColor = Color.FromArgb(200, 210, 225);
            d.Font = new Font("Segoe UI", 10f);
            d.Text = todas[i].Primary ? "Pantalla principal" : b.Width + " × " + b.Height;
            f.Controls.Add(n);
            f.Controls.Add(d);

            System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
            t.Interval = 2000;
            Form esta = f;
            t.Tick += delegate { t.Stop(); t.Dispose(); esta.Close(); };
            f.Show();
            t.Start();
        }
    }

    public static string Nombre(Screen[] todas, int i)
    {
        return "Pantalla " + (i + 1) + (todas[i].Primary ? " (principal)" : "");
    }
}

// Inicio automático con Windows (clave Run del usuario actual).
static class Inicio
{
    const string CLAVE = @"Software\Microsoft\Windows\CurrentVersion\Run", NOMBRE = "RelojBackground";
    const string NOMBRE_ANTIGUO = "OverlayReloj"; // nombre de las primeras versiones

    // Acceso directo de versiones anteriores en la carpeta Inicio.
    static string AccesoAntiguo
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Overlay Reloj.lnk"); }
    }

    public static bool Activo()
    {
        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CLAVE))
            if (k != null && (k.GetValue(NOMBRE) != null || k.GetValue(NOMBRE_ANTIGUO) != null)) return true;
        return File.Exists(AccesoAntiguo);
    }

    public static void Establecer(bool activar)
    {
        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CLAVE))
        {
            if (activar) k.SetValue(NOMBRE, "\"" + Application.ExecutablePath + "\"");
            else k.DeleteValue(NOMBRE, false);
            k.DeleteValue(NOMBRE_ANTIGUO, false);
        }
        try { if (File.Exists(AccesoAntiguo)) File.Delete(AccesoAntiguo); } catch { }
    }

    // Si la versión anterior arrancaba con Windows, la nueva ocupa su lugar.
    public static void Migrar()
    {
        try { if (Activo()) Establecer(true); } catch { }
    }
}

static class Win32
{
    public const int WS_EX_LAYERED = 0x80000, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80,
                     WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;
    public const int WM_HOTKEY = 0x0312, WM_DISPLAYCHANGE = 0x007E;
    public const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;
    public const byte AC_SRC_OVER = 0, AC_SRC_ALPHA = 1;
    public const int ULW_ALPHA = 2;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE { public int CX, CY; public SIZE(int cx, int cy) { CX = cx; CY = cy; } }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
        IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);

    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")] public static extern short GetKeyState(int vKey);
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
