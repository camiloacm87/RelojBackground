// Ventana de configuración del reloj, con aspecto de Windows 11:
// tema claro u oscuro según el sistema, color de acento de Windows, esquinas redondeadas
// y controles propios (interruptores, deslizadores, botones segmentados, miniaturas de los diseños).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ---------- Tema visual ----------

static class Tema
{
    public static readonly bool Oscuro;
    public static readonly Color Fondo, Superficie, Relleno, RellenoHover, Borde, Separador, Texto, Texto2,
                                 Acento, AcentoHover, AcentoPulsado, SobreAcento, Error, Escritorio;
    public static readonly Font Normal, Pequena, Titulo, Grande, Enorme;

    static Tema()
    {
        Oscuro = LeerOscuro();
        Color acento = LeerAcento();
        if (Oscuro)
        {
            Fondo = Color.FromArgb(32, 32, 32);
            Superficie = Color.FromArgb(43, 43, 43);
            Relleno = Color.FromArgb(55, 55, 55);
            RellenoHover = Color.FromArgb(64, 64, 64);
            Borde = Color.FromArgb(68, 68, 68);
            Separador = Color.FromArgb(29, 29, 29);
            Texto = Color.FromArgb(255, 255, 255);
            Texto2 = Color.FromArgb(170, 170, 170);
            Acento = Mezclar(acento, Color.White, 0.35); // en oscuro Windows usa una versión más clara del acento
            Error = Color.FromArgb(255, 153, 164);
        }
        else
        {
            Fondo = Color.FromArgb(243, 243, 243);
            Superficie = Color.FromArgb(255, 255, 255);
            Relleno = Color.FromArgb(246, 246, 246);
            RellenoHover = Color.FromArgb(236, 236, 236);
            Borde = Color.FromArgb(222, 222, 222);
            Separador = Color.FromArgb(234, 234, 234);
            Texto = Color.FromArgb(26, 26, 26);
            Texto2 = Color.FromArgb(96, 96, 96);
            Acento = Mezclar(acento, Color.Black, 0.08);
            Error = Color.FromArgb(196, 43, 28);
        }
        AcentoHover = Mezclar(Acento, Oscuro ? Color.Black : Color.White, 0.12);
        AcentoPulsado = Mezclar(Acento, Oscuro ? Color.Black : Color.White, 0.25);
        SobreAcento = Luminancia(Acento) > 0.55 ? Color.FromArgb(20, 20, 20) : Color.White;
        Escritorio = Color.FromArgb(68, 68, 65);

        string familia = Existe("Segoe UI Variable Text") ? "Segoe UI Variable Text" : "Segoe UI";
        Normal = new Font(familia, 9.75f);
        Pequena = new Font(familia, 8.75f);
        Titulo = new Font("Segoe UI Semibold", 10.5f);
        Grande = new Font("Segoe UI Semibold", 15f);
        Enorme = new Font("Segoe UI Semibold", 24f);
    }

    static bool LeerOscuro()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                return v is int && (int)v == 0;
            }
        }
        catch { return false; }
    }

    // Color de acento elegido en Personalización → Colores (guardado como 0xAABBGGRR).
    static Color LeerAcento()
    {
        try
        {
            using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
            {
                object v = k == null ? null : k.GetValue("AccentColor");
                if (v is int)
                {
                    int abgr = (int)v;
                    return Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                }
            }
        }
        catch { }
        return Color.FromArgb(0, 103, 192);
    }

    static bool Existe(string familia)
    {
        try { using (new FontFamily(familia)) return true; }
        catch { return false; }
    }

    public static Color Mezclar(Color a, Color b, double t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    static double Luminancia(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255; }

    public static GraphicsPath Redondeado(RectangleF r, float radio)
    {
        GraphicsPath gp = new GraphicsPath();
        float d = Math.Min(radio * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { gp.AddRectangle(r); return gp; }
        gp.AddArc(r.X, r.Y, d, d, 180, 90);
        gp.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        gp.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        gp.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        gp.CloseFigure();
        return gp;
    }

    public static void Rellenar(Graphics g, RectangleF r, float radio, Color relleno, Color borde, float grosor)
    {
        using (GraphicsPath p = Redondeado(r, radio))
        {
            if (!relleno.IsEmpty) using (SolidBrush b = new SolidBrush(relleno)) g.FillPath(b, p);
            if (!borde.IsEmpty) using (Pen pen = new Pen(borde, grosor)) g.DrawPath(pen, p);
        }
    }

    // Barra de título oscura, esquinas redondeadas y título del color del fondo (Windows 11).
    public static void AplicarVentana(Form f)
    {
        try
        {
            int oscuro = Oscuro ? 1 : 0;
            Dwm.DwmSetWindowAttribute(f.Handle, 20, ref oscuro, 4);
            int esquinas = 2;
            Dwm.DwmSetWindowAttribute(f.Handle, 33, ref esquinas, 4);
            int titulo = ColorTranslator.ToWin32(Fondo);
            Dwm.DwmSetWindowAttribute(f.Handle, 35, ref titulo, 4);
        }
        catch { } // en Windows 10 algunas opciones no existen: se ignoran
    }
}

static class Dwm
{
    [DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int atributo, ref int valor, int tamano);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    static extern int SetWindowTheme(IntPtr hwnd, string app, string id);

    // Barras de desplazamiento oscuras en modo oscuro (Windows 10 1809 o superior).
    public static void TemaBarras(IntPtr hwnd)
    {
        try { SetWindowTheme(hwnd, Tema.Oscuro ? "DarkMode_Explorer" : "Explorer", null); } catch { }
    }
}

// ---------- Controles propios ----------

abstract class ControlModerno : Control
{
    public Color Fondo = Tema.Superficie; // color sobre el que está el control
    protected bool encima, pulsado;

    protected ControlModerno()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        Cursor = Cursors.Hand;
        Font = Tema.Normal;
        ForeColor = Tema.Texto;
    }

    protected override void OnMouseEnter(EventArgs e) { encima = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { encima = false; pulsado = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pulsado = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pulsado = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Fondo);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Pintar(g);
    }

    protected abstract void Pintar(Graphics g);

    protected void TextoCentrado(Graphics g, string s, Font f, Rectangle r, Color c)
    {
        TextRenderer.DrawText(g, s, f, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                             TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

// Interruptor de encendido/apagado.
class Interruptor : ControlModerno
{
    bool marcado;
    public event EventHandler Cambio;

    public Interruptor() { Size = new Size(44, 24); }

    public bool Marcado
    {
        get { return marcado; }
        set
        {
            if (marcado == value) return;
            marcado = value;
            Invalidate();
            if (Cambio != null) Cambio(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e) { Focus(); Marcado = !Marcado; base.OnClick(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Marcado = !Marcado; base.OnKeyDown(e); }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(1, (Height - 20) / 2f, 40, 20);
        if (marcado) Tema.Rellenar(g, r, 10, encima ? Tema.AcentoHover : Tema.Acento, Color.Empty, 1);
        else Tema.Rellenar(g, r, 10, encima ? Tema.RellenoHover : Tema.Relleno, Tema.Texto2, 1);
        float d = pulsado ? 15 : encima ? 14 : 12;
        float cx = marcado ? r.Right - 10 : r.Left + 10, cy = r.Top + 10;
        using (SolidBrush b = new SolidBrush(marcado ? Tema.SobreAcento : Tema.Texto2)) g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
    }
}

// Botón que se queda pulsado (por ejemplo "Segundos").
class Chip : ControlModerno
{
    bool marcado;
    public event EventHandler Cambio;

    public Chip(string texto)
    {
        Text = texto;
        Size = new Size(TextRenderer.MeasureText("✓  " + texto, Tema.Normal).Width + 22, 30);
    }

    public bool Marcado
    {
        get { return marcado; }
        set
        {
            if (marcado == value) return;
            marcado = value;
            Invalidate();
            if (Cambio != null) Cambio(this, EventArgs.Empty);
        }
    }

    protected override void OnClick(EventArgs e) { Focus(); Marcado = !Marcado; base.OnClick(e); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) Marcado = !Marcado; base.OnKeyDown(e); }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (marcado) Tema.Rellenar(g, r, r.Height / 2, pulsado ? Tema.AcentoPulsado : encima ? Tema.AcentoHover : Tema.Acento, Color.Empty, 1);
        else Tema.Rellenar(g, r, r.Height / 2, encima ? Tema.RellenoHover : Tema.Relleno, Tema.Borde, 1);
        TextoCentrado(g, (marcado ? "✓  " : "") + Text, Font, ClientRectangle, marcado ? Tema.SobreAcento : Tema.Texto);
    }
}

// Deslizador con pista redondeada y pulgar al estilo de Windows 11.
class Deslizador : ControlModerno
{
    int minimo = 0, maximo = 100, valor;
    bool arrastrando;
    public int Paso = 5;
    public event EventHandler Cambio;

    public Deslizador(int min, int max) { minimo = min; maximo = max; valor = min; Size = new Size(200, 28); }

    public int Valor
    {
        get { return valor; }
        set
        {
            int v = Math.Max(minimo, Math.Min(maximo, value));
            if (v == valor) return;
            valor = v;
            Invalidate();
            if (Cambio != null) Cambio(this, EventArgs.Empty);
        }
    }

    float X0 { get { return 11; } }
    float X1 { get { return Width - 11; } }
    float Posicion(int v) { return X0 + (X1 - X0) * (v - minimo) / Math.Max(1f, maximo - minimo); }
    int ValorEn(float x) { return (int)Math.Round(minimo + (x - X0) / (X1 - X0) * (maximo - minimo)); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Enabled) return;
        Focus();
        arrastrando = true;
        Capture = true;
        Valor = ValorEn(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (arrastrando) Valor = ValorEn(e.X);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        arrastrando = false;
        Capture = false;
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Enabled) Valor += e.Delta > 0 ? Paso : -Paso;
    }

    protected override bool IsInputKey(Keys k)
    {
        return k == Keys.Left || k == Keys.Right || k == Keys.Up || k == Keys.Down || base.IsInputKey(k);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Valor -= Paso;
        if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Valor += Paso;
        base.OnKeyDown(e);
    }

    protected override void Pintar(Graphics g)
    {
        float cy = Height / 2f, px = Posicion(valor);
        Color acento = Enabled ? Tema.Acento : Tema.Texto2;
        using (Pen pista = new Pen(Tema.Oscuro ? Color.FromArgb(120, 120, 120) : Color.FromArgb(150, 150, 150), 4))
        using (Pen lleno = new Pen(acento, 4))
        {
            pista.StartCap = pista.EndCap = lleno.StartCap = lleno.EndCap = LineCap.Round;
            g.DrawLine(pista, X0, cy, X1, cy);
            if (px > X0 + 0.5f) g.DrawLine(lleno, X0, cy, px, cy);
        }
        const float D = 20;
        RectangleF pulgar = new RectangleF(px - D / 2, cy - D / 2, D, D);
        using (SolidBrush b = new SolidBrush(Tema.Oscuro ? Color.FromArgb(69, 69, 69) : Color.White)) g.FillEllipse(b, pulgar);
        using (Pen p = new Pen(Tema.Borde)) g.DrawEllipse(p, pulgar);
        float d = !Enabled ? 8 : arrastrando ? 10 : encima ? 14 : 12;
        using (SolidBrush b = new SolidBrush(acento)) g.FillEllipse(b, px - d / 2, cy - d / 2, d, d);
    }
}

// Grupo de botones donde solo uno está elegido (Arriba | Centro | Abajo).
class Segmentado : ControlModerno
{
    string[] items = new string[0];
    int indice = -1, encimaDe = -1;
    public event EventHandler Cambio;

    public Segmentado(params string[] opciones) { items = opciones; Size = new Size(270, 32); }

    public string[] Items
    {
        get { return items; }
        set { items = value ?? new string[0]; Invalidate(); }
    }

    public int Indice
    {
        get { return indice; }
        set
        {
            if (value == indice) return;
            indice = value;
            Invalidate();
            if (Cambio != null) Cambio(this, EventArgs.Empty);
        }
    }

    int IndiceEn(int x)
    {
        if (items.Length == 0) return -1;
        return Math.Max(0, Math.Min(items.Length - 1, x * items.Length / Math.Max(1, Width)));
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int i = IndiceEn(e.X);
        if (i != encimaDe) { encimaDe = i; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { encimaDe = -1; base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        Indice = IndiceEn(e.X);
    }

    protected override bool IsInputKey(Keys k) { return k == Keys.Left || k == Keys.Right || base.IsInputKey(k); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Left && indice > 0) Indice = indice - 1;
        if (e.KeyCode == Keys.Right && indice < items.Length - 1) Indice = indice + 1;
        base.OnKeyDown(e);
    }

    protected override void Pintar(Graphics g)
    {
        Tema.Rellenar(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 6, Tema.Relleno, Tema.Borde, 1);
        if (items.Length == 0) return;
        float w = (Width - 6) / (float)items.Length;
        for (int i = 0; i < items.Length; i++)
        {
            RectangleF s = new RectangleF(3 + i * w, 3, w, Height - 6);
            Color texto = Tema.Texto;
            if (i == indice)
            {
                Tema.Rellenar(g, s, 4, Tema.Acento, Color.Empty, 1);
                texto = Tema.SobreAcento;
            }
            else if (i == encimaDe) Tema.Rellenar(g, s, 4, Tema.RellenoHover, Color.Empty, 1);
            TextoCentrado(g, items[i], Font, Rectangle.Round(s), texto);
        }
    }
}

// Círculo de color para elegir la paleta.
class Muestra : ControlModerno
{
    public Color Color = Color.Empty;
    public bool Arcoiris, Personal, Seleccionada;

    public Muestra() { Size = new Size(32, 32); }

    protected override void Pintar(Graphics g)
    {
        RectangleF anillo = new RectangleF(1.5f, 1.5f, Width - 4, Height - 4);
        if (Seleccionada) using (Pen p = new Pen(Tema.Texto, 2)) g.DrawEllipse(p, anillo);
        else if (encima) using (Pen p = new Pen(Tema.Texto2, 1)) g.DrawEllipse(p, anillo);

        RectangleF c = RectangleF.Inflate(anillo, -4, -4);
        if (Arcoiris)
        {
            using (LinearGradientBrush lg = new LinearGradientBrush(c, Color.Red, Color.Blue, 45f))
            {
                ColorBlend cb = new ColorBlend();
                cb.Colors = new Color[] { Paleta.HSV(0, .65, 1), Paleta.HSV(60, .65, 1), Paleta.HSV(140, .65, 1),
                                          Paleta.HSV(200, .65, 1), Paleta.HSV(260, .65, 1), Paleta.HSV(320, .65, 1) };
                cb.Positions = new float[] { 0f, .2f, .4f, .6f, .8f, 1f };
                lg.InterpolationColors = cb;
                g.FillEllipse(lg, c);
            }
        }
        else if (Personal && Color.IsEmpty)
        {
            using (SolidBrush b = new SolidBrush(Tema.Relleno)) g.FillEllipse(b, c);
            using (Pen p = new Pen(Tema.Borde)) g.DrawEllipse(p, c);
        }
        else
        {
            using (SolidBrush b = new SolidBrush(Color)) g.FillEllipse(b, c);
            using (Pen p = new Pen(Color.FromArgb(40, Tema.Oscuro ? Color.White : Color.Black))) g.DrawEllipse(p, c);
        }
        if (Personal)
        {
            Color mas = Color.IsEmpty ? Tema.Texto : (Color.GetBrightness() > 0.6f ? Color.Black : Color.White);
            using (Pen p = new Pen(mas, 1.6f))
            {
                float cx = c.X + c.Width / 2, cy = c.Y + c.Height / 2;
                g.DrawLine(p, cx - 5, cy, cx + 5, cy);
                g.DrawLine(p, cx, cy - 5, cx, cy + 5);
            }
        }
    }
}

// Miniatura en vivo de un diseño del reloj.
class TarjetaDiseno : ControlModerno
{
    public readonly int Diseno;
    public bool Seleccionada;
    readonly Opciones op;

    public TarjetaDiseno(Opciones o, int diseno, string nombre)
    {
        op = o;
        Diseno = diseno;
        Text = nombre;
        Fondo = Tema.Fondo;
        Size = new Size(85, 88);
    }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(1, 1, Width - 2.5f, Height - 2.5f);
        Color borde = Seleccionada ? Tema.Acento : encima ? Tema.Texto2 : Tema.Borde;
        Tema.Rellenar(g, r, 8, encima && !Seleccionada ? Tema.RellenoHover : Tema.Superficie, borde, Seleccionada ? 2 : 1);

        RectangleF area = new RectangleF(6, 6, Width - 12, Height - 34);
        Tema.Rellenar(g, area, 5, Tema.Escritorio, Color.Empty, 1);
        Size b = Reloj.Base(Diseno);
        float k = Math.Min((area.Width - 6) / b.Width, (area.Height - 6) / b.Height);
        GraphicsState estado = g.Save();
        g.SetClip(area);
        g.TranslateTransform(area.X + (area.Width - b.Width * k) / 2, area.Y + (area.Height - b.Height * k) / 2);
        g.ScaleTransform(k, k);
        Opciones o = op.Clonar();
        o.Diseno = Diseno;
        Reloj.Dibujar(g, o, DateTime.Now);
        g.Restore(estado);

        TextoCentrado(g, Text, Tema.Pequena, new Rectangle(2, Height - 27, Width - 4, 22), Seleccionada ? Tema.Texto : Tema.Texto2);
    }
}

// Botón redondeado: principal (color de acento) o secundario.
class BotonModerno : ControlModerno, IButtonControl
{
    public bool Principal;
    public DialogResult DialogResult { get; set; }

    public BotonModerno(string texto, bool principal)
    {
        Text = texto;
        Principal = principal;
        Size = new Size(Math.Max(100, TextRenderer.MeasureText(texto, Tema.Normal).Width + 32), 34);
    }

    public void NotifyDefault(bool valor) { }
    public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) PerformClick(); base.OnKeyDown(e); }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        if (Principal)
        {
            Tema.Rellenar(g, r, 6, pulsado ? Tema.AcentoPulsado : encima ? Tema.AcentoHover : Tema.Acento, Color.Empty, 1);
            TextoCentrado(g, Text, Font, ClientRectangle, Tema.SobreAcento);
        }
        else
        {
            Tema.Rellenar(g, r, 6, pulsado ? Tema.Borde : encima ? Tema.RellenoHover : Tema.Relleno, Tema.Borde, 1);
            TextoCentrado(g, Text, Font, ClientRectangle, Enabled ? Tema.Texto : Tema.Texto2);
        }
        if (Focused && !Principal) Tema.Rellenar(g, RectangleF.Inflate(r, -2, -2), 5, Color.Empty, Tema.Acento, 1);
    }
}

// Lista desplegable (como un ComboBox) con el aspecto de la app.
class Desplegable : ControlModerno
{
    readonly string[] items;
    int indice;
    public event EventHandler Cambio;

    public Desplegable(params string[] opciones) { items = opciones; Size = new Size(200, 32); }

    public int Indice
    {
        get { return indice; }
        set
        {
            if (value == indice || value < 0 || value >= items.Length) return;
            indice = value;
            Invalidate();
            if (Cambio != null) Cambio(this, EventArgs.Empty);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        Abrir();
    }

    protected override bool IsInputKey(Keys k) { return k == Keys.Up || k == Keys.Down || base.IsInputKey(k); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up) Indice = indice - 1;
        if (e.KeyCode == Keys.Down) Indice = indice + 1;
        if (e.KeyCode == Keys.Space || (e.Alt && e.KeyCode == Keys.Down)) Abrir();
        base.OnKeyDown(e);
    }

    void Abrir()
    {
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new MenuTema();
        menu.Font = Tema.Normal;
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = true;
        for (int i = 0; i < items.Length; i++)
        {
            int k = i;
            ToolStripMenuItem it = new ToolStripMenuItem(items[i]);
            it.Checked = i == indice;
            it.Padding = new Padding(0, 4, 0, 4);
            it.Click += delegate { Indice = k; };
            menu.Items.Add(it);
        }
        menu.MinimumSize = new Size(Width, 0);
        menu.Closed += delegate { BeginInvoke(new MethodInvoker(menu.Dispose)); };
        menu.Show(this, new Point(0, Height + 2));
    }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Tema.Rellenar(g, r, 6, encima ? Tema.RellenoHover : Tema.Relleno, Focused ? Tema.Acento : Tema.Borde, 1);
        TextRenderer.DrawText(g, items[indice], Font, new Rectangle(10, 0, Width - 34, Height), Tema.Texto,
                              TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        using (Pen p = new Pen(Tema.Texto2, 1.6f)) // flecha hacia abajo
        {
            float cx = Width - 17, cy = Height / 2f;
            g.DrawLines(p, new PointF[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
        }
    }
}

// Colores de los menús desplegables según el tema (claro u oscuro).
class MenuTema : ToolStripProfessionalRenderer
{
    public MenuTema() : base(new ColoresMenu()) { RoundedEdges = false; }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = Tema.Texto;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        Rectangle r = e.ImageRectangle;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (Pen p = new Pen(Tema.Acento, 2))
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            e.Graphics.DrawLines(p, new PointF[] { new PointF(cx - 5, cy), new PointF(cx - 1.5f, cy + 3.5f), new PointF(cx + 5, cy - 4) });
        }
    }
}

class ColoresMenu : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground { get { return Tema.Superficie; } }
    public override Color MenuBorder { get { return Tema.Borde; } }
    public override Color MenuItemBorder { get { return Tema.RellenoHover; } }
    public override Color MenuItemSelected { get { return Tema.RellenoHover; } }
    public override Color MenuItemSelectedGradientBegin { get { return Tema.RellenoHover; } }
    public override Color MenuItemSelectedGradientEnd { get { return Tema.RellenoHover; } }
    public override Color ImageMarginGradientBegin { get { return Tema.Superficie; } }
    public override Color ImageMarginGradientMiddle { get { return Tema.Superficie; } }
    public override Color ImageMarginGradientEnd { get { return Tema.Superficie; } }
    public override Color CheckBackground { get { return Tema.Superficie; } }
    public override Color CheckSelectedBackground { get { return Tema.RellenoHover; } }
    public override Color CheckPressedBackground { get { return Tema.RellenoHover; } }
}

// Calendario para elegir un rango de fechas: el primer clic marca el inicio y el segundo el final.
class Calendario : ControlModerno
{
    static readonly System.Globalization.CultureInfo ES = new System.Globalization.CultureInfo("es-ES");
    static readonly string[] DIAS = { "L", "M", "X", "J", "V", "S", "D" };
    const int ARRIBA = 40, FILA_DIAS = 26, CELDA_ALTO = 34;

    DateTime mes;
    public DateTime Desde, Hasta;
    bool eligiendoFinal;
    Point encimaDe = new Point(-1, -1);
    public event EventHandler Cambio;

    public Calendario(DateTime desde, DateTime hasta)
    {
        Desde = desde.Date;
        Hasta = hasta.Date;
        mes = new DateTime(Desde.Year, Desde.Month, 1);
        Size = new Size(280, ARRIBA + FILA_DIAS + 6 * CELDA_ALTO + 4);
    }

    float AnchoCelda { get { return Width / 7f; } }

    // Primer día que se ve en la cuadrícula (lunes de la semana del día 1).
    DateTime PrimeroVisible { get { return mes.AddDays(-(((int)mes.DayOfWeek + 6) % 7)); } }

    Rectangle FlechaAtras { get { return new Rectangle(4, 4, 32, 32); } }
    Rectangle FlechaAdelante { get { return new Rectangle(Width - 36, 4, 32, 32); } }

    bool Celda(Point p, out DateTime dia)
    {
        dia = DateTime.MinValue;
        int y0 = ARRIBA + FILA_DIAS;
        if (p.Y < y0) return false;
        int fila = (p.Y - y0) / CELDA_ALTO, col = (int)(p.X / AnchoCelda);
        if (fila > 5 || col > 6 || col < 0) return false;
        dia = PrimeroVisible.AddDays(fila * 7 + col);
        return true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Point p = new Point(e.X, e.Y);
        if (p != encimaDe) { encimaDe = p; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { encimaDe = new Point(-1, -1); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (FlechaAtras.Contains(e.Location)) { mes = mes.AddMonths(-1); Invalidate(); return; }
        if (FlechaAdelante.Contains(e.Location)) { mes = mes.AddMonths(1); Invalidate(); return; }
        DateTime dia;
        if (!Celda(e.Location, out dia)) return;
        if (!eligiendoFinal) { Desde = Hasta = dia; eligiendoFinal = true; }
        else
        {
            if (dia < Desde) { Hasta = Desde; Desde = dia; } else Hasta = dia;
            eligiendoFinal = false;
        }
        if (dia.Month != mes.Month) mes = new DateTime(dia.Year, dia.Month, 1);
        Invalidate();
        if (Cambio != null) Cambio(this, EventArgs.Empty);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        mes = mes.AddMonths(e.Delta > 0 ? -1 : 1);
        Invalidate();
    }

    protected override void Pintar(Graphics g)
    {
        // Cabecera: mes y flechas
        string titulo = mes.ToString("MMMM yyyy", ES);
        titulo = char.ToUpper(titulo[0]) + titulo.Substring(1);
        TextoCentrado(g, titulo, Tema.Titulo, new Rectangle(36, 4, Width - 72, 32), Tema.Texto);
        foreach (Rectangle f in new Rectangle[] { FlechaAtras, FlechaAdelante })
        {
            if (f.Contains(encimaDe)) Tema.Rellenar(g, f, 6, Tema.RellenoHover, Color.Empty, 1);
            bool atras = f == FlechaAtras;
            float cx = f.X + f.Width / 2f, cy = f.Y + f.Height / 2f, d = atras ? 3 : -3;
            using (Pen p = new Pen(Tema.Texto, 1.8f))
                g.DrawLines(p, new PointF[] { new PointF(cx + d, cy - 6), new PointF(cx - d, cy), new PointF(cx + d, cy + 6) });
        }

        float w = AnchoCelda;
        for (int c = 0; c < 7; c++)
            TextoCentrado(g, DIAS[c], Tema.Pequena, new Rectangle((int)(c * w), ARRIBA, (int)w, FILA_DIAS), Tema.Texto2);

        DateTime inicio = PrimeroVisible, hoy = DateTime.Today;
        int y0 = ARRIBA + FILA_DIAS;
        for (int i = 0; i < 42; i++)
        {
            DateTime dia = inicio.AddDays(i);
            RectangleF celda = new RectangleF((i % 7) * w, y0 + (i / 7) * CELDA_ALTO, w, CELDA_ALTO);
            bool enRango = dia >= Desde && dia <= Hasta, extremo = dia == Desde || dia == Hasta;
            RectangleF circulo = new RectangleF(celda.X + (w - 30) / 2, celda.Y + 2, 30, 30);

            // Franja del rango, de un extremo a otro
            if (enRango && Desde != Hasta)
            {
                float x1 = dia == Desde ? celda.X + w / 2 : celda.X, x2 = dia == Hasta ? celda.X + w / 2 : celda.Right;
                using (SolidBrush b = new SolidBrush(Color.FromArgb(Tema.Oscuro ? 70 : 50, Tema.Acento)))
                    g.FillRectangle(b, x1, circulo.Y, x2 - x1, circulo.Height);
            }
            Color texto = dia.Month == mes.Month ? Tema.Texto : Tema.Texto2;
            if (extremo)
            {
                using (SolidBrush b = new SolidBrush(Tema.Acento)) g.FillEllipse(b, circulo);
                texto = Tema.SobreAcento;
            }
            else if (celda.Contains(encimaDe))
                using (SolidBrush b = new SolidBrush(Tema.RellenoHover)) g.FillEllipse(b, circulo);
            if (dia == hoy && !extremo) using (Pen p = new Pen(Tema.Acento, 1.5f)) g.DrawEllipse(p, circulo);
            TextoCentrado(g, dia.Day.ToString(), Font, Rectangle.Round(circulo), texto);
        }
    }
}

// Ventana pequeña con el calendario y los botones Aceptar / Cancelar.
class VentanaRango : Form
{
    public readonly Calendario Cal;

    public VentanaRango(DateTime desde, DateTime hasta)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Tema.Superficie;
        Font = Tema.Normal;
        KeyPreview = true;

        Cal = new Calendario(desde, hasta);
        Cal.Location = new Point(12, 10);
        Controls.Add(Cal);

        Label ayuda = new Label();
        ayuda.Text = "Haz clic en el primer día y luego en el último.";
        ayuda.Font = Tema.Pequena;
        ayuda.ForeColor = Tema.Texto2;
        ayuda.BackColor = Tema.Superficie;
        ayuda.SetBounds(12, Cal.Bottom + 4, Cal.Width, 20);
        Controls.Add(ayuda);

        BotonModerno aceptar = new BotonModerno("Aceptar", true), cancelar = new BotonModerno("Cancelar", false);
        aceptar.Fondo = cancelar.Fondo = Tema.Superficie;
        aceptar.Size = cancelar.Size = new Size(100, 32);
        aceptar.Location = new Point(Cal.Right - 100, ayuda.Bottom + 8);
        cancelar.Location = new Point(aceptar.Left - 108, ayuda.Bottom + 8);
        aceptar.Click += delegate { DialogResult = DialogResult.OK; };
        cancelar.Click += delegate { DialogResult = DialogResult.Cancel; };
        Controls.Add(aceptar);
        Controls.Add(cancelar);
        AcceptButton = aceptar;
        CancelButton = cancelar;

        ClientSize = new Size(Cal.Right + 12, aceptar.Bottom + 12);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Tema.AplicarVentana(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using (Pen p = new Pen(Tema.Borde)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }
}

// Botón dividido: la parte izquierda hace la acción principal y la flecha de la derecha abre un menú
// con más opciones (las crea la función Opciones cada vez que se abre).
class BotonDividido : ControlModerno
{
    const int ANCHO_FLECHA = 30;
    public event EventHandler Principal;
    public Func<ToolStripItem[]> Opciones;
    int zonaEncima = -1; // 0 = botón, 1 = flecha

    public BotonDividido(string texto)
    {
        Text = texto;
        Size = new Size(TextRenderer.MeasureText(texto, Tema.Normal).Width + 28 + ANCHO_FLECHA, 32);
    }

    int Zona(int x) { return x >= Width - ANCHO_FLECHA ? 1 : 0; }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int z = Zona(e.X);
        if (z != zonaEncima) { zonaEncima = z; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { zonaEncima = -1; base.OnMouseLeave(e); }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || !Enabled || !ClientRectangle.Contains(e.Location)) return;
        if (Zona(e.X) == 1) AbrirMenu();
        else if (Principal != null) Principal(this, EventArgs.Empty);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && Principal != null) Principal(this, EventArgs.Empty);
        if (e.Alt && e.KeyCode == Keys.Down) AbrirMenu();
        base.OnKeyDown(e);
    }

    public void AbrirMenu()
    {
        if (Opciones == null) return;
        ContextMenuStrip menu = new ContextMenuStrip();
        menu.Renderer = new MenuTema();
        menu.Font = Tema.Normal;
        menu.ShowImageMargin = false;
        menu.ShowCheckMargin = true;
        menu.Items.AddRange(Opciones());
        foreach (ToolStripItem it in menu.Items)
        {
            it.Padding = new Padding(0, 3, 0, 3);
            ToolStripMenuItem mi = it as ToolStripMenuItem;
            if (mi != null && mi.HasDropDownItems)
            {
                ((ToolStripDropDownMenu)mi.DropDown).ShowImageMargin = false;
                ((ToolStripDropDownMenu)mi.DropDown).ShowCheckMargin = true;
            }
        }
        menu.Closed += delegate { BeginInvoke(new MethodInvoker(menu.Dispose)); };
        menu.Show(this, new Point(Width - menu.PreferredSize.Width, Height + 2));
    }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Tema.Rellenar(g, r, 6, Tema.Relleno, Tema.Borde, 1);
        if (zonaEncima >= 0)
        {
            RectangleF z = zonaEncima == 0 ? new RectangleF(1.5f, 1.5f, Width - ANCHO_FLECHA - 2.5f, Height - 3.5f)
                                           : new RectangleF(Width - ANCHO_FLECHA, 1.5f, ANCHO_FLECHA - 1.5f, Height - 3.5f);
            Tema.Rellenar(g, z, 5, pulsado ? Tema.Borde : Tema.RellenoHover, Color.Empty, 1);
        }
        using (Pen sep = new Pen(Tema.Borde)) g.DrawLine(sep, Width - ANCHO_FLECHA, 6, Width - ANCHO_FLECHA, Height - 6);
        TextoCentrado(g, Text, Font, new Rectangle(0, 0, Width - ANCHO_FLECHA, Height), Enabled ? Tema.Texto : Tema.Texto2);
        using (Pen p = new Pen(Tema.Texto2, 1.6f))
        {
            float cx = Width - ANCHO_FLECHA / 2f, cy = Height / 2f;
            g.DrawLines(p, new PointF[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
        }
    }
}

// Casilla de texto con borde redondeado (para escribir nombres y enlaces).
class MarcoTexto : ControlModerno
{
    public readonly TextBox Caja = new TextBox();

    public MarcoTexto()
    {
        Size = new Size(300, 32);
        Cursor = Cursors.IBeam;
        Caja.BorderStyle = BorderStyle.None;
        Caja.BackColor = Tema.Relleno;
        Caja.ForeColor = Tema.Texto;
        Caja.Font = Tema.Normal;
        Caja.GotFocus += delegate { Invalidate(); };
        Caja.LostFocus += delegate { Invalidate(); };
        Controls.Add(Caja);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    public override string Text { get { return Caja.Text; } set { Caja.Text = value; } }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int h = Caja.PreferredHeight;
        Caja.SetBounds(10, (Height - h) / 2, Width - 20, h);
    }

    protected override void OnClick(EventArgs e) { Caja.Focus(); base.OnClick(e); }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Tema.Rellenar(g, r, 5, Tema.Relleno, Caja.Focused ? Tema.Acento : Tema.Borde, 1);
        if (Caja.Focused) using (Pen p = new Pen(Tema.Acento, 2)) g.DrawLine(p, 5, Height - 1.5f, Width - 6, Height - 1.5f);
    }
}

// Recuadro de solo lectura con la hora de un evento del calendario (mismo aspecto que el selector de hora).
class CajaHora : ControlModerno
{
    public CajaHora(string texto) { Text = texto; Size = new Size(120, 40); Cursor = Cursors.Default; TabStop = false; }

    protected override void Pintar(Graphics g)
    {
        Tema.Rellenar(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 6, Tema.Relleno, Tema.Borde, 1);
        TextoCentrado(g, Text, Text.Length <= 5 ? Tema.Grande : Tema.Normal, ClientRectangle, Tema.Texto);
    }
}

// Ventana para conectar un calendario. Según el servicio:
// - Google / Outlook / iCloud / Otro: se pega el enlace iCal privado y se comprueba descargándolo.
// - Exportar: la app abre la exportación de Google Calendar en el navegador y lee el archivo descargado.
// - Imagen: se recorta la pantalla o se sube una imagen del calendario; los eventos se reconocen con el OCR de Windows.
class VentanaConectar : Form
{
    static readonly string[] ETIQUETAS = { "Google", "Exportar", "Outlook", "iCloud", "Imagen", "Otro" };
    static readonly string[] PASOS = {
        "En Google Calendar (en la web): ⚙ Configuración → en «Configuración de mis calendarios» elige el calendario → " +
        "«Integrar el calendario» → copia la «Dirección secreta en formato iCal» (termina en basic.ics).",
        "Para cuentas que no tienen dirección secreta (por ejemplo, del trabajo). Al sincronizar, la app abre Google Calendar " +
        "en tu navegador, que descarga una copia de tus calendarios (la misma de Configuración → Importar y exportar → Exportar); " +
        "la app la lee y la borra. Debes tener la sesión de Google iniciada en el navegador.",
        "En Outlook (en la web): ⚙ Configuración → Calendario → Calendarios compartidos → «Publicar un calendario»: " +
        "elige el calendario y «Puede ver todos los detalles» → Publicar → copia el vínculo ICS.",
        "En el iPhone o la Mac, app Calendario: toca ⓘ junto al calendario → activa «Calendario público» → " +
        "copia el enlace (empieza por webcal://).",
        "Abre tu calendario en la pantalla (vista Semana, Día o Programación) y recórtalo, o sube una captura guardada. " +
        "La app lee las fechas, horas y títulos, y te deja revisarlos antes de agregarlos. No se conecta a internet.",
        "Pega el enlace en formato iCal (.ics) de tu calendario. También sirve uno que empiece por webcal://."
    };
    static readonly string[] PAGINAS = {
        "https://calendar.google.com/calendar/r/settings", "", "https://outlook.live.com/calendar/0/options/calendar/SharedCalendars", "", "", ""
    };

    readonly Segmentado servicio = new Segmentado(ETIQUETAS);
    readonly Label pasos = new Label(), estado = new Label(), lEnlace = new Label();
    readonly MarcoTexto nombre = new MarcoTexto(), enlace = new MarcoTexto();
    readonly BotonModerno abrir = new BotonModerno("Abrir la página", false), conectar = new BotonModerno("Conectar", true),
                          cancelar = new BotonModerno("Cancelar", false),
                          recortar = new BotonModerno("Recortar la pantalla", true), subir = new BotonModerno("Subir una imagen", false);
    readonly List<string> nombresUsados;
    public ConexionCalendario Resultado;
    public List<EventoCalendario> EventosImagen; // con el servicio Imagen: los eventos elegidos en la revisión
    public int EventosEncontrados;
    bool nombreEditado;

    int Indice { get { return servicio.Indice; } }

    public VentanaConectar(List<string> usados)
    {
        nombresUsados = usados;
        Text = "Conectar calendario";
        Icon = Iconos.Grande;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        ForeColor = Tema.Texto;
        Font = Tema.Normal;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(600, 440);

        Etiqueta(new Label(), "Servicio", 24, 20, 200, 22, Tema.Titulo, Tema.Texto);
        servicio.Fondo = Tema.Fondo;
        servicio.SetBounds(24, 46, 552, 34);
        servicio.Indice = 0;
        servicio.Cambio += delegate { ElegirServicio(); };
        Controls.Add(servicio);

        Etiqueta(pasos, "", 24, 92, 552, 64, Tema.Normal, Tema.Texto2);
        abrir.Fondo = Tema.Fondo;
        abrir.SetBounds(24, 160, 150, 30);
        abrir.Click += delegate
        {
            string url = PAGINAS[Indice];
            if (url != "") try { System.Diagnostics.Process.Start(url); } catch { }
        };
        Controls.Add(abrir);

        Etiqueta(new Label(), "Nombre", 24, 206, 200, 22, Tema.Titulo, Tema.Texto);
        nombre.Fondo = Tema.Fondo;
        nombre.SetBounds(24, 232, 552, 32);
        nombre.Caja.TextChanged += delegate { if (nombre.Caja.Focused) nombreEditado = true; };
        Controls.Add(nombre);

        Etiqueta(lEnlace, "Enlace iCal (privado)", 24, 278, 400, 22, Tema.Titulo, Tema.Texto);
        enlace.Fondo = Tema.Fondo;
        enlace.SetBounds(24, 304, 552, 32);
        enlace.Caja.TextChanged += delegate { estado.Text = ""; };
        Controls.Add(enlace);

        // Servicio Imagen: en lugar del enlace, los dos botones
        recortar.Fondo = subir.Fondo = Tema.Fondo;
        recortar.SetBounds(24, 290, 200, 36);
        subir.SetBounds(234, 290, 180, 36);
        recortar.Click += delegate { DesdeImagen(true); };
        subir.Click += delegate { DesdeImagen(false); };
        Controls.Add(recortar);
        Controls.Add(subir);

        Etiqueta(estado, "", 24, 344, 552, 40, Tema.Pequena, Tema.Texto2);

        conectar.Fondo = cancelar.Fondo = Tema.Fondo;
        conectar.SetBounds(466, 392, 110, 34);
        cancelar.SetBounds(346, 392, 110, 34);
        conectar.Click += delegate { Conectar(); };
        cancelar.Click += delegate { DialogResult = DialogResult.Cancel; };
        Controls.Add(conectar);
        Controls.Add(cancelar);
        AcceptButton = conectar;
        CancelButton = cancelar;

        ElegirServicio();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Tema.AplicarVentana(this);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        enlace.Caja.Focus();
    }

    Label Etiqueta(Label l, string texto, int x, int y, int w, int h, Font f, Color c)
    {
        l.Text = texto;
        l.Font = f;
        l.ForeColor = c;
        l.BackColor = Tema.Fondo;
        l.SetBounds(x, y, w, h);
        Controls.Add(l);
        return l;
    }

    void ElegirServicio()
    {
        int i = Indice;
        bool imagen = i == ConexionCalendario.IMAGEN, exportar = i == ConexionCalendario.EXPORTAR;
        pasos.Text = PASOS[i];
        abrir.Visible = PAGINAS[i] != "";
        abrir.Text = i == ConexionCalendario.GOOGLE ? "Abrir Google Calendar" : "Abrir Outlook";
        abrir.Width = TextRenderer.MeasureText(abrir.Text, Tema.Normal).Width + 32;
        lEnlace.Visible = enlace.Visible = !imagen;
        recortar.Visible = subir.Visible = imagen;
        conectar.Visible = !imagen;
        AcceptButton = imagen ? (IButtonControl)recortar : conectar;
        lEnlace.Text = exportar ? "Cuenta de Google en el navegador (opcional, por ejemplo tu correo del trabajo)" : "Enlace iCal (privado)";
        estado.ForeColor = Tema.Texto2;
        estado.Text = imagen ? "Consejo: recorta solo la zona del calendario y que se vean el día, la hora y el título de cada evento." :
                      exportar ? "Se abrirá el navegador una vez para comprobarlo." :
                      "El enlace funciona como una contraseña: solo se guarda en esta PC.";
        string base_ = i == ConexionCalendario.IMAGEN ? "Desde imagen" : i == ConexionCalendario.OTRO ? "Calendario" : ConexionCalendario.SERVICIOS[i];
        if (!nombreEditado) nombre.Text = imagen ? base_ : NombreLibre(base_);
    }

    // "Google", "Google 2", "Google 3"…
    string NombreLibre(string base_)
    {
        string n = base_;
        for (int k = 2; nombresUsados.Contains(n); k++) n = base_ + " " + k;
        return n;
    }

    void Conectar()
    {
        bool exportar = Indice == ConexionCalendario.EXPORTAR;
        string texto = enlace.Text.Trim(), nom = nombre.Text.Trim();
        if (!exportar && texto == "") { Error("Pega el enlace iCal del calendario."); return; }
        if (nom == "") nom = NombreLibre(ConexionCalendario.SERVICIOS[Indice]);
        if (nombresUsados.Contains(nom)) { Error("Ya hay un calendario llamado «" + nom + "». Elige otro nombre."); return; }
        string url = exportar ? ICal.UrlExportacion(texto) : ICal.Normalizar(texto);

        conectar.Enabled = cancelar.Enabled = false;
        estado.ForeColor = Tema.Texto2;
        estado.Text = exportar ? "Se abrió tu navegador: esperando la descarga del calendario…" : "Conectando…";
        Cursor = Cursors.WaitCursor;
        System.Threading.ThreadPool.QueueUserWorkItem(delegate
        {
            string error = null;
            int n = 0;
            try
            {
                n = (exportar ? ICal.DescargarConNavegador(url, nom, DateTime.Today, DateTime.Today.AddDays(8))
                              : ICal.Descargar(url, nom, DateTime.Today, DateTime.Today.AddDays(8))).Count;
            }
            catch (Exception ex) { error = ex.Message; }
            try
            {
                BeginInvoke(new MethodInvoker(delegate
                {
                    Cursor = Cursors.Default;
                    conectar.Enabled = cancelar.Enabled = true;
                    if (error != null) { Error("No se pudo conectar: " + error); return; }
                    Resultado = new ConexionCalendario();
                    Resultado.Servicio = ConexionCalendario.SERVICIOS[Indice];
                    Resultado.Nombre = nom;
                    Resultado.Url = url;
                    EventosEncontrados = n;
                    DialogResult = DialogResult.OK;
                }));
            }
            catch { } // la ventana se cerró
        });
    }

    // Servicio Imagen: recorta o sube la imagen, la lee y deja revisar los eventos.
    void DesdeImagen(bool recortarPantalla)
    {
        string nom = nombre.Text.Trim();
        if (nom == "") nom = "Desde imagen";
        List<EventoCalendario> eventos = Reconocimiento.Pedir(this, recortarPantalla, nom);
        if (eventos == null) return;
        Resultado = new ConexionCalendario();
        Resultado.Servicio = ConexionCalendario.SERVICIOS[ConexionCalendario.IMAGEN];
        Resultado.Nombre = nom;
        Resultado.Url = ConexionCalendario.URL_IMAGEN;
        EventosImagen = eventos;
        EventosEncontrados = eventos.Count;
        DialogResult = DialogResult.OK;
    }

    void Error(string texto)
    {
        estado.ForeColor = Tema.Error;
        estado.Text = texto;
    }
}

// Reconocer eventos en una imagen: pedirla (recorte o archivo), leerla con el OCR y revisar el resultado.
static class Reconocimiento
{
    // Devuelve los eventos elegidos en la revisión, o null si se canceló.
    public static List<EventoCalendario> Pedir(Form duenio, bool recortar, string fuente)
    {
        Bitmap imagen = recortar ? RecortePantalla.Capturar(duenio) : Abrir(duenio);
        if (imagen == null) return null;
        List<EventoCalendario> eventos;
        Cursor antes = duenio.Cursor;
        duenio.Cursor = Cursors.WaitCursor;
        try
        {
            using (imagen) eventos = LectorImagen.Reconocer(LectorImagen.LeerTexto(imagen), DateTime.Now, fuente);
        }
        catch (Exception ex)
        {
            duenio.Cursor = antes;
            MessageBox.Show(duenio, "No se pudo leer la imagen: " + ex.Message, Program.NOMBRE, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }
        duenio.Cursor = antes;
        using (VentanaRevision v = new VentanaRevision(eventos))
            return v.ShowDialog(duenio) == DialogResult.OK ? v.Elegidos : null;
    }

    static Bitmap Abrir(Form duenio)
    {
        using (OpenFileDialog d = new OpenFileDialog())
        {
            d.Title = "Elige una imagen de tu calendario";
            d.Filter = "Imágenes|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff|Todos los archivos|*.*";
            if (d.ShowDialog(duenio) != DialogResult.OK) return null;
            try
            {
                using (Image i = Image.FromFile(d.FileName)) return new Bitmap(i);
            }
            catch (Exception ex)
            {
                MessageBox.Show(duenio, "No se pudo abrir la imagen: " + ex.Message, Program.NOMBRE, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
        }
    }
}

// Pantalla para recortar: se oscurece todo y se arrastra para elegir la zona del calendario (Esc cancela).
class RecortePantalla : Form
{
    readonly Bitmap captura;
    Point inicio;
    Rectangle seleccion;
    bool arrastrando;
    Bitmap resultado;

    public static Bitmap Capturar(Form duenio)
    {
        // Se ocultan un momento las ventanas de la app para que no salgan en la captura.
        List<Form> ocultas = new List<Form>();
        for (Form f = duenio; f != null; f = f.Owner) ocultas.Add(f);
        foreach (Form f in Application.OpenForms) if (f.Visible && f.TopLevel && !ocultas.Contains(f) && f is Configuracion) ocultas.Add(f);
        Dictionary<Form, double> opacidad = new Dictionary<Form, double>();
        foreach (Form f in ocultas) { opacidad[f] = f.Opacity; f.Opacity = 0; }
        Application.DoEvents();
        System.Threading.Thread.Sleep(250);

        Rectangle v = SystemInformation.VirtualScreen;
        Bitmap captura = new Bitmap(v.Width, v.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(captura)) g.CopyFromScreen(v.Location, Point.Empty, v.Size);

        Bitmap res = null;
        try
        {
            using (RecortePantalla r = new RecortePantalla(captura, v))
                if (r.ShowDialog() == DialogResult.OK) res = r.resultado;
        }
        finally
        {
            captura.Dispose();
            foreach (Form f in ocultas) f.Opacity = opacidad[f];
        }
        return res;
    }

    RecortePantalla(Bitmap c, Rectangle zona)
    {
        captura = c;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = zona;
        TopMost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        DoubleBuffered = true;
    }

    protected override void OnShown(EventArgs e) { base.OnShown(e); Activate(); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { DialogResult = DialogResult.Cancel; return; }
        arrastrando = true;
        inicio = e.Location;
        seleccion = new Rectangle(e.Location, Size.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!arrastrando) return;
        seleccion = Rectangle.FromLTRB(Math.Min(inicio.X, e.X), Math.Min(inicio.Y, e.Y), Math.Max(inicio.X, e.X), Math.Max(inicio.Y, e.Y));
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (!arrastrando) return;
        arrastrando = false;
        if (seleccion.Width < 30 || seleccion.Height < 30) { seleccion = Rectangle.Empty; Invalidate(); return; }
        resultado = captura.Clone(seleccion, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        DialogResult = DialogResult.OK;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.DrawImageUnscaled(captura, 0, 0);
        using (SolidBrush velo = new SolidBrush(Color.FromArgb(130, 0, 0, 0)))
        using (Region fuera = new Region(ClientRectangle))
        {
            if (!seleccion.IsEmpty) fuera.Exclude(seleccion);
            g.FillRegion(velo, fuera);
        }
        if (!seleccion.IsEmpty)
            using (Pen p = new Pen(Tema.Acento, 2)) g.DrawRectangle(p, seleccion);

        // Instrucciones arriba, en el centro de la pantalla principal
        Rectangle principal = Screen.PrimaryScreen.Bounds;
        principal.Offset(-Bounds.X, -Bounds.Y);
        string texto = "Arrastra para recortar tu calendario  ·  Esc para cancelar";
        Size t = TextRenderer.MeasureText(texto, Tema.Titulo);
        RectangleF caja = new RectangleF(principal.X + (principal.Width - t.Width - 40) / 2f, principal.Y + 24, t.Width + 40, 40);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Tema.Rellenar(g, caja, 20, Color.FromArgb(235, 32, 32, 32), Color.Empty, 1);
        TextRenderer.DrawText(g, texto, Tema.Titulo, Rectangle.Round(caja), Color.White,
                              TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

// Revisión de los eventos reconocidos en una imagen: se pueden quitar y corregir el título antes de agregarlos.
class VentanaRevision : Form
{
    readonly List<EventoCalendario> eventos;
    readonly List<Interruptor> incluir = new List<Interruptor>();
    readonly List<MarcoTexto> titulos = new List<MarcoTexto>();
    readonly BotonModerno agregar = new BotonModerno("Agregar", true);
    public List<EventoCalendario> Elegidos = new List<EventoCalendario>();
    static readonly System.Globalization.CultureInfo ES = new System.Globalization.CultureInfo("es-ES");

    public VentanaRevision(List<EventoCalendario> lista)
    {
        eventos = lista;
        Text = "Eventos reconocidos";
        Icon = Iconos.Grande;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        ForeColor = Tema.Texto;
        Font = Tema.Normal;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(640, 480);

        Label titulo = new Label(), ayuda = new Label();
        Etiqueta(titulo, eventos.Count == 0 ? "No se reconoció ningún evento" : "Se reconocieron " + eventos.Count + (eventos.Count == 1 ? " evento" : " eventos"),
                 24, 18, 592, 24, Tema.Titulo, Tema.Texto);
        Etiqueta(ayuda, eventos.Count == 0
            ? "Prueba a recortar solo la zona del calendario, con la vista Semana, Día o Programación, y que se vean el día, la hora y el título de cada evento."
            : "Revisa lo que se leyó: desactiva los que no quieras y corrige el título si hace falta. El aviso sonará antes de cada evento con hora.",
            24, 44, 592, 40, Tema.Normal, Tema.Texto2);

        Panel lista_ = new Panel();
        lista_.SetBounds(24, 92, 592, 324);
        lista_.AutoScroll = true;
        lista_.BackColor = Tema.Fondo;
        lista_.HandleCreated += delegate { Dwm.TemaBarras(lista_.Handle); };
        Controls.Add(lista_);

        if (eventos.Count > 0)
        {
            const int alto = 52, ancho = 574;
            Tarjeta t = new Tarjeta();
            t.SetBounds(0, 0, ancho, eventos.Count * alto + 2);
            lista_.Controls.Add(t);
            for (int i = 0; i < eventos.Count; i++)
            {
                EventoCalendario e = eventos[i];
                int y = i * alto;
                Interruptor inc = new Interruptor();
                inc.Marcado = true;
                inc.Fondo = Tema.Superficie;
                inc.Location = new Point(14, y + (alto - inc.Height) / 2);
                inc.Cambio += delegate { Contar(); };
                t.Controls.Add(inc);
                incluir.Add(inc);

                CajaHora hora = new CajaHora(e.TodoElDia ? "Todo el día" : e.Inicio.ToString("HH:mm"));
                hora.Size = new Size(104, 36);
                hora.Fondo = Tema.Superficie;
                hora.Location = new Point(inc.Right + 10, y + (alto - hora.Height) / 2);
                t.Controls.Add(hora);

                Label dia = new Label();
                string nombreDia = e.Inicio.ToString("dddd", ES); // "domingo" → "Dom" (el formato ddd da "do.")
                dia.Text = char.ToUpper(nombreDia[0]) + nombreDia.Substring(1, 2) + " " + e.Inicio.ToString("d MMM", ES).Replace(".", "");
                dia.Font = Tema.Normal;
                dia.ForeColor = Tema.Texto2;
                dia.BackColor = Tema.Superficie;
                dia.TextAlign = ContentAlignment.MiddleLeft;
                dia.SetBounds(hora.Right + 10, y + 1, 90, alto - 2);
                t.Controls.Add(dia);

                MarcoTexto tt = new MarcoTexto();
                tt.Fondo = Tema.Superficie;
                tt.Text = e.Titulo;
                tt.SetBounds(dia.Right + 6, y + (alto - 32) / 2, ancho - dia.Right - 6 - 14, 32);
                t.Controls.Add(tt);
                titulos.Add(tt);
                if (i < eventos.Count - 1) t.Separadores.Add(y + alto);
            }
        }

        BotonModerno cancelar = new BotonModerno("Cancelar", false);
        agregar.Fondo = cancelar.Fondo = Tema.Fondo;
        agregar.SetBounds(476, 432, 140, 34);
        cancelar.SetBounds(356, 432, 110, 34);
        agregar.Click += delegate { Aceptar(); };
        cancelar.Click += delegate { DialogResult = DialogResult.Cancel; };
        Controls.Add(agregar);
        Controls.Add(cancelar);
        AcceptButton = agregar;
        CancelButton = cancelar;
        Contar();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Tema.AplicarVentana(this);
    }

    void Etiqueta(Label l, string texto, int x, int y, int w, int h, Font f, Color c)
    {
        l.Text = texto;
        l.Font = f;
        l.ForeColor = c;
        l.BackColor = Tema.Fondo;
        l.SetBounds(x, y, w, h);
        Controls.Add(l);
    }

    void Contar()
    {
        int n = incluir.FindAll(delegate(Interruptor i) { return i.Marcado; }).Count;
        agregar.Text = n == 0 ? "Agregar" : "Agregar " + n + (n == 1 ? " evento" : " eventos");
        agregar.Enabled = n > 0;
    }

    void Aceptar()
    {
        Elegidos.Clear();
        for (int i = 0; i < eventos.Count; i++)
        {
            if (!incluir[i].Marcado) continue;
            EventoCalendario e = eventos[i];
            string t = titulos[i].Text.Trim();
            if (t != "") e.Titulo = t;
            Elegidos.Add(e);
        }
        DialogResult = DialogResult.OK;
    }
}

// Tarjeta con fondo redondeado; si AltoFila > 0 dibuja separadores entre filas iguales,
// y además dibuja los de la lista Separadores (para filas de distinta altura).
class Tarjeta : Panel
{
    public int AltoFila;
    public readonly List<int> Separadores = new List<int>();

    public Tarjeta()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);
        BackColor = Tema.Fondo;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        Tema.Rellenar(g, new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f), 8, Tema.Superficie, Tema.Borde, 1);
        g.SmoothingMode = SmoothingMode.None;
        using (Pen p = new Pen(Tema.Separador))
        {
            if (AltoFila > 0)
                for (int y = AltoFila; y < Height - 6; y += AltoFila) g.DrawLine(p, 1, y, Width - 2, y);
            foreach (int y in Separadores) g.DrawLine(p, 1, y, Width - 2, y);
        }
    }
}

// Marco redondeado con una casilla que captura atajos de teclado.
class MarcoAtajo : ControlModerno
{
    public readonly CapturaAtajo Caja = new CapturaAtajo();

    public MarcoAtajo()
    {
        Size = new Size(200, 32);
        Caja.BorderStyle = BorderStyle.None;
        Caja.BackColor = Tema.Relleno;
        Caja.ForeColor = Tema.Texto;
        Caja.Font = Tema.Normal;
        Caja.GotFocus += delegate { Invalidate(); };
        Caja.LostFocus += delegate { Invalidate(); };
        Controls.Add(Caja);
        SetStyle(ControlStyles.Selectable, false);
        TabStop = false;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int h = Caja.PreferredHeight;
        Caja.SetBounds(8, (Height - h) / 2, Width - 16, h);
    }

    protected override void OnClick(EventArgs e) { Caja.Focus(); base.OnClick(e); }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Tema.Rellenar(g, r, 5, Tema.Relleno, Caja.Focused ? Tema.Acento : Tema.Borde, Caja.Focused ? 1.5f : 1);
        if (Caja.Focused)
            using (Pen p = new Pen(Tema.Acento, 2)) g.DrawLine(p, 5, Height - 1.5f, Width - 6, Height - 1.5f);
    }
}

// Casilla que muestra un atajo y, al pulsar una combinación de teclas, la captura.
class CapturaAtajo : TextBox
{
    Atajo valor;
    public event EventHandler Cambio;

    public CapturaAtajo()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        TextAlign = HorizontalAlignment.Center;
        Cursor = Cursors.Hand;
    }

    public Atajo Valor
    {
        get { return valor; }
        set
        {
            valor = value;
            Text = Focused ? TextoFoco() : TextoNormal();
            if (Cambio != null) Cambio(this, EventArgs.Empty);
        }
    }

    string TextoNormal() { return valor.Vacio ? "Sin atajo" : valor.Mostrar(); }
    string TextoFoco() { return valor.Vacio ? "Pulsa la combinación…" : valor.Mostrar(); }

    protected override void OnEnter(EventArgs e)
    {
        base.OnEnter(e);
        Text = TextoFoco();
        BeginInvoke(new MethodInvoker(delegate { Select(0, 0); })); // sin texto seleccionado
    }

    protected override void OnLeave(EventArgs e) { base.OnLeave(e); Text = TextoNormal(); }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys tecla = keyData & Keys.KeyCode, mods = keyData & Keys.Modifiers;
        if (tecla == Keys.Tab && (mods == Keys.None || mods == Keys.Shift)) return base.ProcessCmdKey(ref msg, keyData);
        if (tecla == Keys.Escape && mods == Keys.None) { Text = TextoFoco(); return true; } // Esc no cierra la ventana aquí
        if ((tecla == Keys.Back || tecla == Keys.Delete) && mods == Keys.None) { Valor = new Atajo(); return true; }

        uint m = 0;
        if ((mods & Keys.Control) != 0) m |= Win32.MOD_CONTROL;
        if ((mods & Keys.Alt) != 0) m |= Win32.MOD_ALT;
        if ((mods & Keys.Shift) != 0) m |= Win32.MOD_SHIFT;
        if (Win32.GetKeyState(0x5B) < 0 || Win32.GetKeyState(0x5C) < 0) m |= Win32.MOD_WIN;

        bool soloModificador = tecla == Keys.ControlKey || tecla == Keys.ShiftKey || tecla == Keys.Menu
                            || tecla == Keys.LWin || tecla == Keys.RWin;
        if (soloModificador)
        {
            Text = new Atajo(m, Keys.None).MostrarModificadores() + "…";
            return true;
        }

        bool teclaF = tecla >= Keys.F1 && tecla <= Keys.F24;
        if ((m & (Win32.MOD_CONTROL | Win32.MOD_ALT | Win32.MOD_WIN)) == 0 && !teclaF)
        {
            Text = "Usa Ctrl, Alt o Win + tecla";
            return true;
        }
        Valor = new Atajo(m, tecla);
        return true;
    }
}

// Vista previa: el reloj en una pantalla de computadora, y de cerca.
class Vista : Control
{
    readonly Opciones op;

    public Vista(Opciones o)
    {
        op = o;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
    }

    // Arriba: un monitor con el escritorio y el reloj en su tamaño y posición reales.
    // Abajo: el reloj de cerca, para ver los detalles del diseño.
    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.Clear(Tema.Fondo);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        DateTime ahora = DateTime.Now;

        float abajoMonitor = DibujarMonitor(g, ahora);

        // Vista de cerca
        float y0 = abajoMonitor + 18;
        TextRenderer.DrawText(g, "De cerca", Tema.Pequena, new Point(2, (int)y0), Tema.Texto2);
        RectangleF cerca = new RectangleF(0.5f, y0 + 22, Width - 1.5f, Height - y0 - 23);
        if (cerca.Height < 40) return;
        using (GraphicsPath p = Tema.Redondeado(cerca, 10))
        {
            g.SetClip(p);
            using (LinearGradientBrush lg = new LinearGradientBrush(cerca, Color.FromArgb(58, 66, 84), Color.FromArgb(34, 38, 48), 90f))
                g.FillRectangle(lg, cerca);
            Size bs = Reloj.Base(op.Diseno);
            float k = Math.Min((cerca.Width - 24) / bs.Width, (cerca.Height - 24) / bs.Height);
            DibujarReloj(g, new RectangleF(cerca.X + (cerca.Width - bs.Width * k) / 2, cerca.Y + (cerca.Height - bs.Height * k) / 2,
                                           bs.Width * k, bs.Height * k), k, ahora);
            g.ResetClip();
            using (Pen borde = new Pen(Tema.Borde)) g.DrawPath(borde, p);
        }
    }

    // Pantalla donde se verá el reloj (la primera si se eligieron todas).
    Screen PantallaEjemplo()
    {
        foreach (Screen s in Screen.AllScreens) if (s.DeviceName == op.Pantalla) return s;
        return Screen.PrimaryScreen;
    }

    // Dibuja el monitor y devuelve dónde termina su base.
    float DibujarMonitor(Graphics g, DateTime ahora)
    {
        Screen s = PantallaEjemplo();
        Rectangle real = s.Bounds, util = s.WorkingArea;
        const float marco = 9, menton = 16;
        float ancho = Width - 2;
        RectangleF pantalla = new RectangleF(1 + marco, 6 + marco, ancho - 2 * marco, (ancho - 2 * marco) * real.Height / real.Width);
        RectangleF cuerpo = new RectangleF(1, 6, ancho, pantalla.Height + 2 * marco + menton);

        // Pie y base
        float cx = Width / 2f, arriba = cuerpo.Bottom - 2;
        using (GraphicsPath pie = new GraphicsPath())
        {
            pie.AddPolygon(new PointF[] { new PointF(cx - 18, arriba), new PointF(cx + 18, arriba), new PointF(cx + 26, arriba + 36), new PointF(cx - 26, arriba + 36) });
            using (LinearGradientBrush lg = new LinearGradientBrush(new RectangleF(cx - 26, arriba, 52, 36), Color.FromArgb(150, 152, 160), Color.FromArgb(96, 98, 106), 0f))
                g.FillPath(lg, pie);
        }
        Tema.Rellenar(g, new RectangleF(cx - 62, arriba + 34, 124, 9), 4.5f, Color.FromArgb(170, 172, 180), Color.FromArgb(110, 112, 120), 1);

        // Carcasa y pantalla
        Tema.Rellenar(g, cuerpo, 12, Color.FromArgb(26, 27, 31), Color.FromArgb(70, 72, 80), 1);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 92, 100)))
            g.FillEllipse(b, cx - 2.5f, cuerpo.Bottom - menton / 2 - 3.5f, 5, 5);

        GraphicsState estado = g.Save();
        g.SetClip(pantalla);
        // Fondo de escritorio
        using (LinearGradientBrush lg = new LinearGradientBrush(pantalla, Color.FromArgb(32, 78, 140), Color.FromArgb(14, 26, 54), 60f))
            g.FillRectangle(lg, pantalla);
        using (GraphicsPath brillo = new GraphicsPath())
        {
            brillo.AddEllipse(pantalla.X + pantalla.Width * 0.35f, pantalla.Y + pantalla.Height * 0.15f, pantalla.Width * 0.7f, pantalla.Height * 1.1f);
            using (PathGradientBrush pb = new PathGradientBrush(brillo))
            {
                pb.CenterColor = Color.FromArgb(120, 90, 170, 230);
                pb.SurroundColors = new Color[] { Color.FromArgb(0, 90, 170, 230) };
                g.FillPath(pb, brillo);
            }
        }

        float k = pantalla.Width / real.Width; // escala de píxeles reales a la imagen
        // Ventana abierta de ejemplo
        RectangleF ventana = new RectangleF(pantalla.X + pantalla.Width * 0.08f, pantalla.Y + pantalla.Height * 0.14f,
                                            pantalla.Width * 0.5f, pantalla.Height * 0.55f);
        Tema.Rellenar(g, ventana, 3, Color.FromArgb(238, 240, 244), Color.Empty, 1);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(206, 210, 218))) g.FillRectangle(b, ventana.X, ventana.Y, ventana.Width, 7);
        using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 204, 212)))
            for (int i = 0; i < 4; i++) g.FillRectangle(b, ventana.X + 6, ventana.Y + 14 + i * 9, ventana.Width * (i % 2 == 0 ? 0.7f : 0.5f), 4);

        // Barra de tareas (lo que queda fuera del área de trabajo)
        if (util.Bottom < real.Bottom)
        {
            float alto = Math.Max(5, (real.Bottom - util.Bottom) * k);
            RectangleF barra = new RectangleF(pantalla.X, pantalla.Bottom - alto, pantalla.Width, alto);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(220, 24, 26, 32))) g.FillRectangle(b, barra);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(200, 255, 255, 255)))
                for (int i = -2; i <= 2; i++) g.FillRectangle(b, cx + i * 8 - 2.5f, barra.Y + alto / 2 - 2.5f, 5, 5);
        }

        // El reloj, con el tamaño y la posición que tendrá de verdad
        bool completa = op.PantallaCompleta;
        Size bs = Reloj.Base(op.Diseno);
        Rectangle area = completa ? real : util;
        float escala = completa ? Math.Min(area.Width / (float)bs.Width, area.Height / (float)bs.Height) : op.Escala;
        float w = bs.Width * escala, h = bs.Height * escala, m = completa ? 0 : 20;
        float x = op.PosH == 0 ? area.Left + m : op.PosH == 1 ? area.Left + (area.Width - w) / 2 : area.Right - w - m;
        float y = op.PosV == 0 ? area.Top + m : op.PosV == 1 ? area.Top + (area.Height - h) / 2 : area.Bottom - h - m;
        DibujarReloj(g, new RectangleF(pantalla.X + (x - real.Left) * k, pantalla.Y + (y - real.Top) * k, w * k, h * k), escala * k, ahora);
        g.Restore(estado);

        return arriba + 43;
    }

    // Dibuja el reloj en un rectángulo, aplicando la opacidad elegida.
    void DibujarReloj(Graphics g, RectangleF r, float k, DateTime ahora)
    {
        int w = (int)Math.Ceiling(r.Width), h = (int)Math.Ceiling(r.Height);
        if (w < 1 || h < 1) return;
        using (Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            using (Graphics gb = Graphics.FromImage(bmp))
            {
                gb.ScaleTransform(k, k);
                Reloj.Dibujar(gb, op, ahora);
            }
            ColorMatrix cm = new ColorMatrix();
            cm.Matrix33 = op.Opacidad;
            using (ImageAttributes ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                g.DrawImage(bmp, new Rectangle((int)r.X, (int)r.Y, w, h), 0, 0, w, h, GraphicsUnit.Pixel, ia);
            }
        }
    }
}

// Selector de hora "07 : 30". Se cambia con la rueda del ratón, las flechas, escribiendo los números
// o haciendo clic en la mitad de arriba (+1) o de abajo (−1) de cada número.
class SelectorHora : ControlModerno
{
    int hora, minuto, parte, encimaDe = -1;
    string escrito = "";
    public event EventHandler Cambio;

    public SelectorHora() { Size = new Size(120, 40); }

    public int Hora { get { return hora; } set { hora = ((value % 24) + 24) % 24; Invalidate(); } }
    public int Minuto { get { return minuto; } set { minuto = ((value % 60) + 60) % 60; Invalidate(); } }

    Rectangle Zona(int p) { return p == 0 ? new Rectangle(4, 0, 50, Height) : new Rectangle(Width - 54, 0, 50, Height); }
    int ParteEn(int x) { return x < Width / 2 ? 0 : 1; }

    void Cambiar(int p, int delta)
    {
        if (p == 0) Hora = hora + delta;
        else Minuto = minuto + delta;
        if (Cambio != null) Cambio(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        int p = ParteEn(e.X);
        if (p != encimaDe) { encimaDe = p; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e) { encimaDe = -1; base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        parte = ParteEn(e.X);
        escrito = "";
        Cambiar(parte, e.Y < Height / 2 ? 1 : -1);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Cambiar(ParteEn(e.X), e.Delta > 0 ? 1 : -1);
    }

    protected override bool IsInputKey(Keys k)
    {
        return k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right || base.IsInputKey(k);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up) Cambiar(parte, 1);
        if (e.KeyCode == Keys.Down) Cambiar(parte, -1);
        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right) { parte = 1 - parte; escrito = ""; Invalidate(); }
        base.OnKeyDown(e);
    }

    // Escribir dos cifras pone ese valor (y de la hora salta a los minutos).
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (!char.IsDigit(e.KeyChar)) return;
        escrito += e.KeyChar;
        int v = int.Parse(escrito);
        if (parte == 0) Hora = Math.Min(23, v); else Minuto = Math.Min(59, v);
        if (escrito.Length == 2)
        {
            escrito = "";
            if (parte == 0) parte = 1;
        }
        if (Cambio != null) Cambio(this, EventArgs.Empty);
    }

    protected override void OnLostFocus(EventArgs e) { escrito = ""; base.OnLostFocus(e); }

    protected override void Pintar(Graphics g)
    {
        RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        Tema.Rellenar(g, r, 6, Tema.Relleno, Focused ? Tema.Acento : Tema.Borde, 1);
        for (int p = 0; p < 2; p++)
        {
            Rectangle z = Zona(p);
            bool activa = Focused && parte == p;
            if (activa) Tema.Rellenar(g, new RectangleF(z.X + 2, 4, z.Width - 4, Height - 8), 4, Tema.RellenoHover, Color.Empty, 1);
            TextoCentrado(g, (p == 0 ? hora : minuto).ToString("00"), Tema.Grande, z, Tema.Texto);
            if (encimaDe == p) // flechas pequeñas para indicar que se puede subir o bajar
                using (SolidBrush b = new SolidBrush(Tema.Texto2))
                {
                    float cx = z.X + z.Width / 2f;
                    g.FillPolygon(b, new PointF[] { new PointF(cx - 4, 7), new PointF(cx + 4, 7), new PointF(cx, 3) });
                    g.FillPolygon(b, new PointF[] { new PointF(cx - 4, Height - 7), new PointF(cx + 4, Height - 7), new PointF(cx, Height - 3) });
                }
            if (activa) using (Pen pen = new Pen(Tema.Acento, 2)) g.DrawLine(pen, z.X + 10, Height - 5, z.Right - 10, Height - 5);
        }
        TextoCentrado(g, ":", Tema.Grande, new Rectangle(Width / 2 - 14, -2, 28, Height), Tema.Texto2);
    }
}

// ---------- Ventana de configuración ----------

class Configuracion : Form
{
    readonly Overlay overlay;
    readonly Opciones op;
    readonly Opciones original;
    bool guardado, cargando = true, sincronizando;

    readonly Vista vista;
    readonly System.Windows.Forms.Timer refresco = new System.Windows.Forms.Timer();
    readonly Segmentado pestanas = new Segmentado("Apariencia", "Alarmas", "Cronómetro", "Atajos");
    readonly Panel pagApariencia = new Panel(), pagAlarmas = new Panel(), pagCrono = new Panel(), pagAtajos = new Panel();
    const int PAG_APARIENCIA = 0, PAG_ALARMAS = 1, PAG_CRONO = 2, PAG_ATAJOS = 3;
    readonly Tarjeta tarjetaAlarmas = new Tarjeta(), tarjetaCrono = new Tarjeta(), tarjetaEventos = new Tarjeta();
    readonly Label lSinAlarmas = new Label(), lCrono = new Label(), lCalTitulo = new Label(), lCalEstado = new Label();
    readonly Panel listaAlarmas = new Panel(); // alarmas + eventos del calendario, con barra de desplazamiento
    readonly BotonDividido bSincronizar = new BotonDividido("Sincronizar calendario");
    const int ANCHO_LISTA = 532; // deja sitio para la barra de desplazamiento
    readonly Chip chSonido = new Chip("Sonido");
    readonly BotonModerno bAgregar = new BotonModerno("+  Agregar alarma", false),
                          bCrono = new BotonModerno("Iniciar", true), bReiniciar = new BotonModerno("Reiniciar", false);
    const int MAX_ALARMAS = 5, ALTO_ALARMA = 52, ALTO_ALARMA_RANGO = 94;
    readonly List<TarjetaDiseno> tarjetas = new List<TarjetaDiseno>();
    readonly List<Muestra> muestras = new List<Muestra>();
    readonly Muestra muestraOtro = new Muestra();
    readonly Chip ch12 = new Chip("12 horas"), chSeg = new Chip("Segundos"), chFecha = new Chip("Fecha"),
                  chCompleta = new Chip("Pantalla completa");
    readonly Deslizador dTam = new Deslizador(20, 500), dOpa = new Deslizador(10, 100);
    readonly Label lTam = new Label(), lOpa = new Label(), lAyuda = new Label(), lAviso = new Label();
    readonly Segmentado sVertical = new Segmentado("Arriba", "Centro", "Abajo"),
                        sHorizontal = new Segmentado("Izquierda", "Centro", "Derecha"),
                        sPantalla = new Segmentado();
    readonly List<string> valoresPantalla = new List<string>();
    readonly Interruptor iInicio = new Interruptor();
    readonly MarcoAtajo[] marcos = new MarcoAtajo[Acciones.Nombres.Length];
    readonly ToolTip ayuda = new ToolTip();

    static readonly string[] NOMBRES_CORTOS = { "Anillo", "Analógico", "Minimalista", "Píldora", "Tres anillos", "Tarjetas" };
    const int ALTO_FILA = 44;

    public Configuracion(Overlay ov, Opciones opciones)
    {
        overlay = ov;
        op = opciones;
        original = op.Clonar();

        Text = Program.NOMBRE + " · Configuración";
        Icon = Iconos.Grande;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        BackColor = Tema.Fondo;
        ForeColor = Tema.Texto;
        Font = Tema.Normal;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(900, 660);

        // Columna izquierda: vista previa
        vista = new Vista(op);
        vista.SetBounds(20, 20, 290, 566);
        Controls.Add(vista);
        Etiqueta(this, lAyuda, "", 22, 596, 290, 44, Tema.Pequena, Tema.Texto2, Tema.Fondo);

        // Columna derecha: pestañas y páginas
        pestanas.Fondo = Tema.Fondo;
        pestanas.SetBounds(330, 20, 480, 34);
        pestanas.Indice = 0;
        pestanas.Cambio += delegate { if (!sincronizando) MostrarPagina(pestanas.Indice); };
        Controls.Add(pestanas);

        foreach (Panel p in new Panel[] { pagApariencia, pagAlarmas, pagCrono, pagAtajos })
        {
            p.SetBounds(330, 70, 550, 520);
            p.BackColor = Tema.Fondo;
            p.Visible = p == pagApariencia;
            Controls.Add(p);
        }
        CrearApariencia();
        CrearAlarmas();
        CrearCronometro();
        CrearAtajos();

        // Botones inferiores
        BotonModerno guardar = new BotonModerno("Guardar", true), cancelar = new BotonModerno("Cancelar", false);
        guardar.Fondo = cancelar.Fondo = Tema.Fondo;
        guardar.SetBounds(770, 606, 110, 34);
        cancelar.SetBounds(650, 606, 110, 34);
        guardar.Click += delegate { Guardar(); };
        cancelar.Click += delegate { Close(); };
        Controls.Add(guardar);
        Controls.Add(cancelar);
        AcceptButton = guardar;
        CancelButton = cancelar;

        refresco.Interval = 100;
        refresco.Tick += delegate
        {
            vista.Invalidate();
            if (pagApariencia.Visible) foreach (TarjetaDiseno t in tarjetas) t.Invalidate();
            if (pagCrono.Visible) ActualizarCronometro();
        };
        refresco.Start();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += PantallasCambiaron;

        cargando = false;
        Sincronizar();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Tema.AplicarVentana(this);
    }

    // ---------- Pestaña Apariencia ----------

    void CrearApariencia()
    {
        Panel pg = pagApariencia;
        Etiqueta(pg, new Label(), "Diseño", 0, 0, 300, 22, Tema.Titulo, Tema.Texto, Tema.Fondo);
        for (int i = 0; i < Reloj.Disenos.Length; i++)
        {
            int indice = i;
            TarjetaDiseno t = new TarjetaDiseno(op, i, NOMBRES_CORTOS[i]);
            t.SetBounds(i * 93, 26, 85, 88);
            t.Click += delegate { op.Diseno = indice; Aplicar(); };
            ayuda.SetToolTip(t, Reloj.Disenos[i]);
            pg.Controls.Add(t);
            tarjetas.Add(t);
        }

        Etiqueta(pg, new Label(), "Color", 0, 126, 300, 22, Tema.Titulo, Tema.Texto, Tema.Fondo);
        for (int i = 0; i < Paleta.Nombres.Length; i++)
        {
            int indice = i;
            Muestra m = new Muestra();
            m.Fondo = Tema.Fondo;
            if (i == Paleta.ARCOIRIS) m.Arcoiris = true;
            else m.Color = Paleta.Principal(i);
            m.SetBounds(i * 38 - 2, 150, 32, 32);
            m.Click += delegate { op.ColorIdx = indice; Aplicar(); };
            ayuda.SetToolTip(m, Paleta.Nombres[i]);
            pg.Controls.Add(m);
            muestras.Add(m);
        }
        muestraOtro.Fondo = Tema.Fondo;
        muestraOtro.Personal = true;
        muestraOtro.SetBounds(Paleta.Nombres.Length * 38 - 2, 150, 32, 32);
        muestraOtro.Click += delegate { ElegirOtroColor(); };
        ayuda.SetToolTip(muestraOtro, "Otro color…");
        pg.Controls.Add(muestraOtro);

        // Tarjeta con las opciones, una por fila
        Tarjeta t2 = new Tarjeta();
        t2.AltoFila = ALTO_FILA;
        t2.SetBounds(0, 194, 550, ALTO_FILA * 7 + 2);
        pg.Controls.Add(t2);

        Fila(t2, 0, "Mostrar");
        Derecha(t2, 0, ch12, chSeg, chFecha);
        ch12.Cambio += delegate { if (!cargando) { op.Formato12 = ch12.Marcado; Aplicar(); } };
        chSeg.Cambio += delegate { if (!cargando) { op.Segundos = chSeg.Marcado; Aplicar(); } };
        chFecha.Cambio += delegate { if (!cargando) { op.Fecha = chFecha.Marcado; Aplicar(); } };

        Fila(t2, 1, "Tamaño");
        lTam.TextAlign = ContentAlignment.MiddleRight;
        Etiqueta(t2, lTam, "", 0, 0, 62, 24, Tema.Normal, Tema.Texto2, Tema.Superficie);
        dTam.Size = new Size(180, 28);
        dTam.Paso = 10;
        Derecha(t2, 1, dTam, lTam, chCompleta);
        dTam.Cambio += delegate { if (!cargando) { op.Escala = dTam.Valor / 100f; Aplicar(); } };
        chCompleta.Cambio += delegate { if (!cargando) { op.PantallaCompleta = chCompleta.Marcado; Aplicar(); } };

        Fila(t2, 2, "Opacidad");
        lOpa.TextAlign = ContentAlignment.MiddleRight;
        Etiqueta(t2, lOpa, "", 0, 0, 62, 24, Tema.Normal, Tema.Texto2, Tema.Superficie);
        dOpa.Size = new Size(180, 28);
        Derecha(t2, 2, dOpa, lOpa);
        dOpa.Cambio += delegate { if (!cargando) { op.Opacidad = dOpa.Valor / 100f; Aplicar(); } };

        Fila(t2, 3, "Posición vertical");
        Derecha(t2, 3, sVertical);
        sVertical.Cambio += delegate { if (!cargando) { op.PosV = sVertical.Indice; Aplicar(); } };

        Fila(t2, 4, "Posición horizontal");
        Derecha(t2, 4, sHorizontal);
        sHorizontal.Cambio += delegate { if (!cargando) { op.PosH = sHorizontal.Indice; Aplicar(); } };

        Fila(t2, 5, "Pantalla");
        BotonModerno identificar = new BotonModerno("Identificar", false);
        identificar.Size = new Size(96, 32);
        identificar.Click += delegate { Identificador.Mostrar(); };
        sPantalla.Size = new Size(250, 32);
        LlenarPantallas();
        Derecha(t2, 5, sPantalla, identificar);
        sPantalla.Cambio += delegate
        {
            if (cargando || sPantalla.Indice < 0) return;
            op.Pantalla = valoresPantalla[sPantalla.Indice];
            Aplicar();
        };

        Fila(t2, 6, "Iniciar con Windows");
        iInicio.Marcado = Inicio.Activo();
        Derecha(t2, 6, iInicio);
    }

    // ---------- Pestaña Atajos ----------

    void CrearAtajos()
    {
        Panel pg = pagAtajos;
        Etiqueta(pg, new Label(), "Atajos de teclado", 0, 0, 400, 22, Tema.Titulo, Tema.Texto, Tema.Fondo);
        Etiqueta(pg, new Label(), "Haz clic en una casilla y pulsa la combinación, por ejemplo Ctrl+Alt+R. " +
                                  "Retroceso o Supr la deja sin atajo. Usa Ctrl, Alt o Win junto con otra tecla.",
                 0, 26, 550, 40, Tema.Normal, Tema.Texto2, Tema.Fondo);

        int n = Acciones.Nombres.Length;
        const int alto = 40; // filas algo más bajas para que quepan todas las acciones
        Tarjeta t = new Tarjeta();
        t.AltoFila = alto;
        t.SetBounds(0, 74, 550, alto * n + 2);
        pg.Controls.Add(t);

        for (int i = 0; i < n; i++)
        {
            int indice = i;
            Fila(t, i, Acciones.Nombres[i]);
            MarcoAtajo m = new MarcoAtajo();
            m.Size = new Size(200, 30);
            marcos[i] = m;
            BotonModerno quitar = new BotonModerno("Quitar", false);
            quitar.Size = new Size(84, 30);
            quitar.Click += delegate { m.Caja.Valor = new Atajo(); };
            Derecha(t, i, m, quitar);
            m.Caja.Cambio += delegate { CambioAtajo(indice); };
        }

        int y = 74 + alto * n + 10;
        Etiqueta(pg, lAviso, "", 0, y, 400, 40, Tema.Normal, Tema.Error, Tema.Fondo);
        BotonModerno restablecer = new BotonModerno("Restablecer", false);
        restablecer.Fondo = Tema.Fondo;
        restablecer.SetBounds(550 - 120, y, 120, 34);
        restablecer.Click += delegate
        {
            op.Atajos = Acciones.PorDefecto();
            lAviso.Text = "";
            Sincronizar();
        };
        pg.Controls.Add(restablecer);
    }

    void CambioAtajo(int i)
    {
        if (sincronizando) return;
        Atajo v = marcos[i].Caja.Valor;
        for (int j = 0; j < op.Atajos.Length; j++)
        {
            if (j == i || v.Vacio || op.Atajos[j].Texto() != v.Texto()) continue;
            lAviso.Text = v.Mostrar() + " ya está en «" + Acciones.Nombres[j] + "».";
            sincronizando = true;
            marcos[i].Caja.Valor = op.Atajos[i];
            sincronizando = false;
            return;
        }
        op.Atajos[i] = v;
        lAviso.Text = "";
        lAyuda.Text = Acciones.Resumen(op.Atajos);
    }

    // En la pestaña de atajos se pausan los del reloj, para poder pulsar las combinaciones.
    void MostrarPagina(int i)
    {
        if (i == PAG_ATAJOS) overlay.AtajosPausados = true;
        else if (!ValidarAtajos())
        {
            sincronizando = true;
            pestanas.Indice = PAG_ATAJOS;
            sincronizando = false;
            return;
        }
        pagApariencia.Visible = i == PAG_APARIENCIA;
        pagAlarmas.Visible = i == PAG_ALARMAS;
        pagCrono.Visible = i == PAG_CRONO;
        pagAtajos.Visible = i == PAG_ATAJOS;
        if (i == PAG_CRONO) ActualizarCronometro();
    }

    // ---------- Pestaña Alarmas ----------

    void CrearAlarmas()
    {
        Panel pg = pagAlarmas;
        Etiqueta(pg, new Label(), "Alarmas", 0, 6, 120, 22, Tema.Titulo, Tema.Texto, Tema.Fondo);

        // Arriba a la derecha: sonido, probar y agregar
        bAgregar.Fondo = Tema.Fondo;
        bAgregar.SetBounds(550 - bAgregar.Width, 0, bAgregar.Width, 32);
        bAgregar.Click += delegate
        {
            if (op.Alarmas.Count >= MAX_ALARMAS) return;
            Alarma a = new Alarma();
            a.Hora = (DateTime.Now.Hour + 1) % 24; // por defecto, a la próxima hora en punto
            op.Alarmas.Add(a);
            ConstruirAlarmas();
        };
        pg.Controls.Add(bAgregar);

        // Sincronizar calendario ▾ (la flecha abre: sincronizar, conectar otro, aviso y eliminar sincronización)
        bSincronizar.Fondo = Tema.Fondo;
        bSincronizar.Location = new Point(bAgregar.Left - 8 - bSincronizar.Width, 0);
        bSincronizar.Principal += delegate
        {
            if (op.Calendarios.Count == 0) ConectarCalendario();
            else if (op.Calendarios.TrueForAll(delegate(ConexionCalendario c) { return c.EsImagen; }))
                ReconocerImagen(true, op.Calendarios[0].Nombre); // solo hay imágenes: no hay nada que descargar
            else overlay.SincronizarCalendarios();
        };
        bSincronizar.Opciones = OpcionesCalendario;
        ayuda.SetToolTip(bSincronizar, "Cargar los eventos de tu calendario (Google, Outlook, iCloud…)");
        pg.Controls.Add(bSincronizar);

        // Lista con desplazamiento: alarmas y, debajo, los eventos del calendario
        listaAlarmas.SetBounds(0, 42, 550, 432);
        listaAlarmas.AutoScroll = true;
        listaAlarmas.BackColor = Tema.Fondo;
        listaAlarmas.HandleCreated += delegate { Dwm.TemaBarras(listaAlarmas.Handle); };
        pg.Controls.Add(listaAlarmas);
        listaAlarmas.Controls.Add(tarjetaAlarmas);
        lSinAlarmas.TextAlign = ContentAlignment.MiddleLeft;
        Etiqueta(tarjetaAlarmas, lSinAlarmas, "No hay alarmas. Pulsa «Agregar alarma» para crear una.",
                 16, 1, 500, ALTO_ALARMA - 2, Tema.Normal, Tema.Texto2, Tema.Superficie);
        Etiqueta(listaAlarmas, lCalTitulo, "Del calendario", 0, 0, ANCHO_LISTA, 22, Tema.Titulo, Tema.Texto, Tema.Fondo);
        Etiqueta(listaAlarmas, lCalEstado, "", 0, 0, ANCHO_LISTA, 20, Tema.Pequena, Tema.Texto2, Tema.Fondo);
        lCalEstado.AutoEllipsis = true;
        listaAlarmas.Controls.Add(tarjetaEventos);

        // Abajo: sonido y probar
        BotonModerno probar = new BotonModerno("Probar", false);
        probar.Fondo = Tema.Fondo;
        probar.SetBounds(550 - 90, 484, 90, 32);
        probar.Click += delegate { overlay.SonarAlarma(DateTime.Now.ToString("HH:mm")); };
        ayuda.SetToolTip(probar, "Hacer sonar la alarma ahora");
        pg.Controls.Add(probar);
        chSonido.Fondo = Tema.Fondo;
        chSonido.Location = new Point(probar.Left - 8 - chSonido.Width, 485);
        chSonido.Cambio += delegate { if (!cargando) op.SonidoAlarma = chSonido.Marcado; };
        ayuda.SetToolTip(chSonido, "Además del despertador animado, suena un pitido");
        pg.Controls.Add(chSonido);

        overlay.CalendarioActualizado += CalendarioActualizado;
        ConstruirAlarmas();
    }

    void CalendarioActualizado(object sender, EventArgs e)
    {
        if (!IsDisposed) ConstruirEventos();
    }

    // Menú de la flecha de "Sincronizar calendario".
    ToolStripItem[] OpcionesCalendario()
    {
        List<ToolStripItem> l = new List<ToolStripItem>();
        int n = op.Calendarios.Count;
        bool conEnlace = op.Calendarios.Exists(delegate(ConexionCalendario c) { return !c.EsImagen; });
        if (conEnlace) l.Add(Opcion("Sincronizar ahora", delegate { overlay.SincronizarCalendarios(); }));
        l.Add(Opcion(n > 0 ? "Conectar otro calendario…" : "Conectar un calendario…", delegate { ConectarCalendario(); }));

        // Reconocer eventos en una imagen (se agregan al calendario "Desde imagen", o al que ya exista de ese tipo)
        ConexionCalendario deImagen = op.Calendarios.Find(delegate(ConexionCalendario c) { return c.EsImagen; });
        string fuente = deImagen != null ? deImagen.Nombre : "Desde imagen";
        ToolStripMenuItem imagen = new ToolStripMenuItem(deImagen != null ? "Reconocer otra imagen" : "Reconocer desde una imagen");
        imagen.DropDownItems.Add(Opcion("Recortar la pantalla…", delegate { ReconocerImagen(true, fuente); }));
        imagen.DropDownItems.Add(Opcion("Subir una imagen…", delegate { ReconocerImagen(false, fuente); }));
        foreach (ToolStripItem it in imagen.DropDownItems) it.Padding = new Padding(0, 3, 0, 3);
        imagen.DropDown.Renderer = new MenuTema();
        l.Add(imagen);
        if (n == 0) return l.ToArray();

        l.Add(new ToolStripSeparator());
        ToolStripMenuItem aviso = new ToolStripMenuItem("Avisar antes de cada evento");
        int[] minutos = { 0, 5, 10, 15, 30, 60 };
        foreach (int m in minutos)
        {
            int k = m;
            ToolStripMenuItem it = Opcion(m == 0 ? "Sin aviso" : m == 60 ? "1 hora antes" : m + " minutos antes", delegate
            {
                op.AvisoCalendario = k;
                ConstruirEventos();
            });
            it.Checked = op.AvisoCalendario == m;
            it.Padding = new Padding(0, 3, 0, 3);
            aviso.DropDownItems.Add(it);
        }
        aviso.DropDown.Renderer = new MenuTema();
        l.Add(aviso);

        l.Add(new ToolStripSeparator());
        foreach (ConexionCalendario c in op.Calendarios)
        {
            ConexionCalendario esta = c;
            l.Add(Opcion("Eliminar sincronización · " + c.Nombre, delegate { EliminarCalendarios(esta); }));
        }
        if (n > 1) l.Add(Opcion("Eliminar todas las sincronizaciones", delegate { EliminarCalendarios(null); }));
        return l.ToArray();
    }

    static ToolStripMenuItem Opcion(string texto, EventHandler accion)
    {
        ToolStripMenuItem it = new ToolStripMenuItem(texto);
        it.Click += accion;
        return it;
    }

    void ConectarCalendario()
    {
        List<string> usados = new List<string>();
        foreach (ConexionCalendario c in op.Calendarios) usados.Add(c.Nombre);
        using (VentanaConectar v = new VentanaConectar(usados))
        {
            if (v.ShowDialog(this) != DialogResult.OK || v.Resultado == null) return;
            if (v.Resultado.EsImagen)
            {
                AgregarDeImagen(v.Resultado.Nombre, v.EventosImagen);
                return;
            }
            op.Calendarios.Add(v.Resultado);
        }
        overlay.SincronizarCalendarios();
        ConstruirEventos();
    }

    // Recorta la pantalla o sube una imagen y agrega los eventos reconocidos al calendario "fuente".
    void ReconocerImagen(bool recortar, string fuente)
    {
        List<EventoCalendario> eventos = Reconocimiento.Pedir(this, recortar, fuente);
        if (eventos != null) AgregarDeImagen(fuente, eventos);
    }

    void AgregarDeImagen(string fuente, List<EventoCalendario> eventos)
    {
        if (!op.Calendarios.Exists(delegate(ConexionCalendario c) { return c.Nombre == fuente; }))
        {
            ConexionCalendario c = new ConexionCalendario();
            c.Servicio = ConexionCalendario.SERVICIOS[ConexionCalendario.IMAGEN];
            c.Nombre = fuente;
            c.Url = ConexionCalendario.URL_IMAGEN;
            op.Calendarios.Add(c);
        }
        foreach (EventoCalendario e in eventos) e.Fuente = fuente;
        overlay.AgregarEventosDeImagen(eventos);
        ConstruirEventos();
    }

    // Quita un calendario (o todos, si es null) y sus eventos de la lista.
    void EliminarCalendarios(ConexionCalendario c)
    {
        if (c == null) op.Calendarios.Clear();
        else op.Calendarios.Remove(c);
        overlay.PodarEventos();
        ConstruirEventos();
    }

    // Sección "Del calendario": estado de la sincronización y eventos de los próximos 7 días.
    void ConstruirEventos()
    {
        tarjetaEventos.SuspendLayout();
        for (int i = tarjetaEventos.Controls.Count - 1; i >= 0; i--)
        {
            Control c = tarjetaEventos.Controls[i];
            tarjetaEventos.Controls.RemoveAt(i);
            c.Dispose();
        }
        tarjetaEventos.Separadores.Clear();

        bool hay = op.Calendarios.Count > 0;
        lCalTitulo.Visible = lCalEstado.Visible = tarjetaEventos.Visible = hay;
        bSincronizar.Enabled = !overlay.Sincronizando;
        if (!hay) { tarjetaEventos.ResumeLayout(); return; }

        List<string> nombres = new List<string>();
        foreach (ConexionCalendario c in op.Calendarios) nombres.Add(c.Nombre);
        lCalTitulo.Text = "Del calendario · " + string.Join(", ", nombres.ToArray());

        DateTime ahora = DateTime.Now, limite = DateTime.Today.AddDays(7);
        List<EventoCalendario> eventos = overlay.Eventos.FindAll(delegate(EventoCalendario e)
        {
            return e.Fin > ahora && e.Inicio < limite && nombres.Contains(e.Fuente);
        });
        if (eventos.Count > 30) eventos.RemoveRange(30, eventos.Count - 30);

        if (overlay.Sincronizando) { lCalEstado.ForeColor = Tema.Texto2; lCalEstado.Text = "Sincronizando…"; }
        else if (overlay.ErrorCalendario != null) { lCalEstado.ForeColor = Tema.Error; lCalEstado.Text = "No se pudo sincronizar · " + overlay.ErrorCalendario.Replace("\n", " · "); }
        else if (!overlay.UltimaSincronizacion.HasValue && op.Calendarios.TrueForAll(delegate(ConexionCalendario c) { return c.EsImagen; }))
        {
            lCalEstado.ForeColor = Tema.Texto2;
            lCalEstado.Text = eventos.Count + (eventos.Count == 1 ? " evento reconocido" : " eventos reconocidos") + " en imágenes · " +
                              (op.AvisoCalendario > 0 ? "aviso " + (op.AvisoCalendario == 60 ? "1 hora" : op.AvisoCalendario + " min") + " antes" : "sin aviso");
        }
        else if (!overlay.UltimaSincronizacion.HasValue) { lCalEstado.ForeColor = Tema.Texto2; lCalEstado.Text = "Pulsa «Sincronizar calendario» para cargar los eventos."; }
        else
        {
            DateTime u = overlay.UltimaSincronizacion.Value;
            lCalEstado.ForeColor = Tema.Texto2;
            lCalEstado.Text = "Sincronizado " + (u.Date == DateTime.Today ? "hoy" : u.Date == DateTime.Today.AddDays(-1) ? "ayer" : "el " + u.ToString("d MMM", ES).Replace(".", "")) +
                              " a las " + u.ToString("HH:mm") + " · " + eventos.Count + (eventos.Count == 1 ? " evento" : " eventos") + " en los próximos 7 días" +
                              (op.AvisoCalendario > 0 ? " · aviso " + (op.AvisoCalendario == 60 ? "1 hora" : op.AvisoCalendario + " min") + " antes" : " · sin aviso");
        }

        const int alto = 52;
        int y = 0;
        if (eventos.Count == 0)
        {
            Label vacio = new Label();
            vacio.TextAlign = ContentAlignment.MiddleLeft;
            Etiqueta(tarjetaEventos, vacio, overlay.UltimaSincronizacion.HasValue ? "No hay eventos en los próximos 7 días." : "Todavía no se han cargado los eventos.",
                     16, 1, 480, alto - 2, Tema.Normal, Tema.Texto2, Tema.Superficie);
            y = alto;
        }
        foreach (EventoCalendario e in eventos)
        {
            CajaHora hora = new CajaHora(e.TodoElDia ? "Todo el día" : e.Inicio.ToString("HH:mm"));
            hora.Location = new Point(14, y + (alto - hora.Height) / 2);
            hora.Fondo = Tema.Superficie;
            tarjetaEventos.Controls.Add(hora);

            Label titulo = new Label(), detalle = new Label();
            titulo.AutoEllipsis = detalle.AutoEllipsis = true;
            Etiqueta(tarjetaEventos, titulo, e.Titulo, hora.Right + 12, y + 7, 300, 20, Tema.Normal, Tema.Texto, Tema.Superficie);
            string dia = e.Inicio.Date == DateTime.Today ? "Hoy" : e.Inicio.Date == DateTime.Today.AddDays(1) ? "Mañana"
                       : Mayuscula(e.Inicio.ToString("dddd d MMM", ES).Replace(".", ""));
            string horas = e.TodoElDia ? "" : " · " + e.Inicio.ToString("HH:mm") + " – " + e.Fin.ToString("HH:mm");
            Etiqueta(tarjetaEventos, detalle, dia + horas + " · " + e.Fuente, hora.Right + 12, y + 27, 300, 18, Tema.Pequena, Tema.Texto2, Tema.Superficie);

            if (!e.TodoElDia && op.AvisoCalendario > 0)
            {
                string clave = e.Clave;
                Interruptor avisar = new Interruptor();
                avisar.Marcado = !op.EventosSilenciados.Contains(clave);
                avisar.Fondo = Tema.Superficie;
                avisar.Location = new Point(ANCHO_LISTA - 16 - avisar.Width, y + (alto - avisar.Height) / 2);
                avisar.Cambio += delegate
                {
                    if (avisar.Marcado) op.EventosSilenciados.Remove(clave);
                    else op.EventosSilenciados.Add(clave);
                };
                ayuda.SetToolTip(avisar, "Hacer sonar el despertador " + (op.AvisoCalendario == 60 ? "1 hora" : op.AvisoCalendario + " minutos") + " antes");
                tarjetaEventos.Controls.Add(avisar);
            }
            y += alto;
            if (e != eventos[eventos.Count - 1]) tarjetaEventos.Separadores.Add(y);
        }

        // (tarjetaAlarmas.Bottom ya tiene en cuenta cuánto se ha desplazado la lista)
        lCalTitulo.SetBounds(0, tarjetaAlarmas.Bottom + 20, ANCHO_LISTA, 22);
        lCalEstado.SetBounds(0, lCalTitulo.Bottom + 2, ANCHO_LISTA, 20);
        tarjetaEventos.SetBounds(0, lCalEstado.Bottom + 6, ANCHO_LISTA, y + 2);
        tarjetaEventos.ResumeLayout();
        tarjetaEventos.Invalidate();
    }

    static readonly System.Globalization.CultureInfo ES = new System.Globalization.CultureInfo("es-ES");
    static string Mayuscula(string s) { return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s; }

    // Rehace las filas de alarmas (al agregar, eliminar, cambiar cuándo suenan o cuando una suena y se desactiva).
    // Cada fila: hora · cuándo suena · interruptor · Eliminar; las de "Rango de fechas" llevan debajo el botón de fechas.
    void ConstruirAlarmas()
    {
        tarjetaAlarmas.SuspendLayout();
        for (int i = tarjetaAlarmas.Controls.Count - 1; i >= 0; i--)
        {
            Control c = tarjetaAlarmas.Controls[i];
            if (c == lSinAlarmas) continue;
            tarjetaAlarmas.Controls.RemoveAt(i);
            c.Dispose();
        }
        tarjetaAlarmas.Separadores.Clear();

        int n = op.Alarmas.Count, y = 0;
        lSinAlarmas.Visible = n == 0;
        tarjetaAlarmas.Width = ANCHO_LISTA;
        for (int i = 0; i < n; i++)
        {
            Alarma a = op.Alarmas[i];
            bool rango = a.Modo == Alarma.RANGO;

            SelectorHora sh = new SelectorHora();
            sh.Hora = a.Hora;
            sh.Minuto = a.Minuto;
            sh.Location = new Point(14, y + (ALTO_ALARMA - sh.Height) / 2);
            sh.Cambio += delegate
            {
                a.Hora = sh.Hora;
                a.Minuto = sh.Minuto;
                Reactivar(a);
                ConstruirEstado();
            };
            tarjetaAlarmas.Controls.Add(sh);

            Desplegable cuando = new Desplegable(Alarma.MODOS);
            cuando.Indice = a.Modo;
            cuando.Location = new Point(sh.Right + 10, y + (ALTO_ALARMA - cuando.Height) / 2);
            cuando.Cambio += delegate
            {
                a.Modo = cuando.Indice;
                if (a.Modo == Alarma.RANGO && a.Hasta.Date < DateTime.Today)
                {
                    a.Desde = DateTime.Today;
                    a.Hasta = DateTime.Today.AddDays(6);
                }
                Reactivar(a);
                // Se rehace la lista (cambia la altura de la fila); con rango, se abre el calendario.
                BeginInvoke(new MethodInvoker(delegate
                {
                    ConstruirAlarmas();
                    if (a.Modo == Alarma.RANGO) ElegirRango(a);
                }));
            };
            ayuda.SetToolTip(cuando, "Qué días suena la alarma");
            tarjetaAlarmas.Controls.Add(cuando);

            BotonModerno eliminar = new BotonModerno("Eliminar", false);
            eliminar.Size = new Size(90, 32);
            eliminar.Location = new Point(ANCHO_LISTA - 16 - eliminar.Width, y + (ALTO_ALARMA - eliminar.Height) / 2);
            // Se rehace la lista después del clic: el botón pulsado se elimina con ella.
            eliminar.Click += delegate { op.Alarmas.Remove(a); BeginInvoke(new MethodInvoker(ConstruirAlarmas)); };
            Interruptor activa = new Interruptor();
            activa.Marcado = a.Activa;
            activa.Location = new Point(eliminar.Left - 12 - activa.Width, y + (ALTO_ALARMA - activa.Height) / 2);
            activa.Cambio += delegate
            {
                if (sincronizando) return;
                a.Activa = activa.Marcado;
                if (a.Activa && a.Modo == Alarma.HOY) a.Fecha = DateTime.Today; // "Solo hoy" vuelve a ser hoy
            };
            ayuda.SetToolTip(activa, "Activar o desactivar esta alarma");
            foreach (ControlModerno c in new ControlModerno[] { cuando, activa, eliminar }) c.Fondo = Tema.Superficie;
            tarjetaAlarmas.Controls.Add(activa);
            tarjetaAlarmas.Controls.Add(eliminar);

            if (rango)
            {
                BotonModerno fechas = new BotonModerno("Del  " + a.RangoTexto(), false);
                fechas.Fondo = Tema.Superficie;
                fechas.Size = new Size(Math.Max(cuando.Width, fechas.Width), 32);
                fechas.Location = new Point(cuando.Left, y + ALTO_ALARMA - 4);
                fechas.Click += delegate { ElegirRango(a); };
                ayuda.SetToolTip(fechas, "Elegir el primer y el último día");
                tarjetaAlarmas.Controls.Add(fechas);
            }

            y += rango ? ALTO_ALARMA_RANGO : ALTO_ALARMA;
            if (i < n - 1) tarjetaAlarmas.Separadores.Add(y);
        }
        // Dentro de la lista con desplazamiento: AutoScrollPosition.Y es negativo si se ha bajado.
        tarjetaAlarmas.SetBounds(0, listaAlarmas.AutoScrollPosition.Y, ANCHO_LISTA, Math.Max(ALTO_ALARMA, y) + 2);
        tarjetaAlarmas.ResumeLayout();
        tarjetaAlarmas.Invalidate();
        bAgregar.Enabled = n < MAX_ALARMAS;
        ConstruirEventos(); // los eventos van debajo de las alarmas
    }

    // Al cambiar una alarma vuelve a quedar activa ("Solo hoy" pasa a ser el día de hoy).
    static void Reactivar(Alarma a)
    {
        a.Activa = true;
        if (a.Modo == Alarma.HOY) a.Fecha = DateTime.Today;
    }

    // Abre el calendario para elegir el rango de días de una alarma.
    void ElegirRango(Alarma a)
    {
        using (VentanaRango v = new VentanaRango(a.Desde, a.Hasta))
        {
            Point p = PointToScreen(new Point(pagAlarmas.Left + 140, pagAlarmas.Top + 60));
            v.Location = p;
            if (v.ShowDialog(this) != DialogResult.OK) return;
            a.Desde = v.Cal.Desde;
            a.Hasta = v.Cal.Hasta;
            a.Activa = true;
        }
        ConstruirAlarmas();
    }

    // ---------- Pestaña Cronómetro ----------

    void CrearCronometro()
    {
        Panel pg = pagCrono;
        Etiqueta(pg, new Label(), "Cronómetro", 0, 6, 300, 22, Tema.Titulo, Tema.Texto, Tema.Fondo);
        tarjetaCrono.AltoFila = 0;
        pg.Controls.Add(tarjetaCrono);
        tarjetaCrono.SetBounds(0, 42, 550, 80);
        lCrono.TextAlign = ContentAlignment.MiddleLeft;
        Etiqueta(tarjetaCrono, lCrono, "00:00.0", 16, 8, 250, 64, Tema.Enorme, Tema.Texto, Tema.Superficie);
        bCrono.Size = new Size(110, 34);
        bReiniciar.Size = new Size(110, 34);
        bCrono.Click += delegate { overlay.CronometroAlternar(); ActualizarCronometro(); };
        bReiniciar.Click += delegate { overlay.CronometroReiniciar(); ActualizarCronometro(); };
        int x = 550 - 16 - 110;
        bReiniciar.Location = new Point(x, 23);
        bCrono.Location = new Point(x - 8 - 110, 23);
        bCrono.Fondo = bReiniciar.Fondo = Tema.Superficie;
        tarjetaCrono.Controls.Add(bCrono);
        tarjetaCrono.Controls.Add(bReiniciar);
        Etiqueta(pg, new Label(), "Mientras tiene tiempo, se ve en una píldora debajo del reloj. Al reiniciarlo desaparece. " +
                                  "También puedes usarlo desde el icono junto al reloj de Windows o con un atajo.",
                 0, 132, 550, 44, Tema.Normal, Tema.Texto2, Tema.Fondo);
    }

    // Una alarma activa queda marcada como activa al cambiar su hora; refresca los interruptores.
    void ConstruirEstado()
    {
        int fila = 0;
        foreach (Control c in tarjetaAlarmas.Controls)
        {
            Interruptor it = c as Interruptor;
            if (it == null) continue;
            if (fila < op.Alarmas.Count)
            {
                bool antes = sincronizando;
                sincronizando = true;
                it.Marcado = op.Alarmas[fila].Activa;
                sincronizando = antes;
            }
            fila++;
        }
    }

    // La llama el reloj cuando una alarma de una sola vez suena y se desactiva.
    public void RefrescarAlarmas()
    {
        if (!IsDisposed) ConstruirAlarmas();
    }

    void ActualizarCronometro()
    {
        TimeSpan t = overlay.TiempoCronometro;
        string s = t.TotalHours >= 1
            ? (int)t.TotalHours + ":" + t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00")
            : t.Minutes.ToString("00") + ":" + t.Seconds.ToString("00");
        lCrono.Text = s + "." + t.Milliseconds / 100;
        lCrono.ForeColor = overlay.CronometroCorriendo ? Tema.Texto : Tema.Texto2;
        string boton = overlay.CronometroCorriendo ? "Pausar" : t.Ticks > 0 ? "Continuar" : "Iniciar";
        if (bCrono.Text != boton) bCrono.Text = boton;
        bReiniciar.Enabled = t.Ticks > 0;
    }

    // Reactiva los atajos; si Windows u otra aplicación ya usa alguno, avisa y no deja salir.
    bool ValidarAtajos()
    {
        if (!overlay.AtajosPausados) return true;
        overlay.AtajosPausados = false;
        List<string> fallidos = overlay.RegistrarAtajos();
        if (fallidos.Count == 0) { lAviso.Text = ""; return true; }
        overlay.AtajosPausados = true;
        lAviso.Text = "Ya los usa Windows u otra aplicación: " + string.Join(", ", fallidos.ToArray()) + ". Elige otra combinación.";
        return false;
    }

    // ---------- Ayudantes de diseño ----------

    Label Etiqueta(Control padre, Label l, string texto, int x, int y, int w, int h, Font f, Color color, Color fondo)
    {
        l.Text = texto;
        l.Font = f;
        l.ForeColor = color;
        l.BackColor = fondo;
        l.SetBounds(x, y, w, h);
        padre.Controls.Add(l);
        return l;
    }

    void Fila(Tarjeta t, int fila, string texto)
    {
        Label l = new Label();
        l.TextAlign = ContentAlignment.MiddleLeft;
        int alto = t.AltoFila > 0 ? t.AltoFila : ALTO_FILA;
        int ancho = TextRenderer.MeasureText(texto, Tema.Normal).Width + 8; // justo el texto, para no tapar los controles
        Etiqueta(t, l, texto, 16, fila * alto + 1, ancho, alto - 2, Tema.Normal, Tema.Texto, Tema.Superficie);
    }

    // Coloca controles alineados a la derecha de una fila (el primero queda más a la izquierda).
    void Derecha(Tarjeta t, int fila, params Control[] controles)
    {
        DerechaFila(t, fila, t.AltoFila > 0 ? t.AltoFila : ALTO_FILA, controles);
    }

    void DerechaFila(Tarjeta t, int fila, int alto, params Control[] controles)
    {
        int x = t.Width - 16;
        for (int i = controles.Length - 1; i >= 0; i--)
        {
            Control c = controles[i];
            x -= c.Width;
            c.Location = new Point(x, fila * alto + (alto - c.Height) / 2);
            ControlModerno cm = c as ControlModerno;
            if (cm != null) cm.Fondo = Tema.Superficie;
            if (c.Parent != t) t.Controls.Add(c);
            x -= 8;
        }
    }

    // ---------- Pantallas ----------

    void LlenarPantallas()
    {
        Screen[] todas = Screen.AllScreens;
        List<string> items = new List<string>();
        valoresPantalla.Clear();
        items.Add("Todas");
        valoresPantalla.Add("*");
        for (int i = 0; i < todas.Length; i++)
        {
            // Nombres cortos para que quepan; con muchos monitores, solo el número.
            items.Add(todas[i].Primary ? "Principal" : todas.Length > 2 ? (i + 1).ToString() : "Pantalla " + (i + 1));
            valoresPantalla.Add(todas[i].DeviceName);
        }
        sPantalla.Items = items.ToArray();
        sPantalla.Font = items.Count > 3 ? Tema.Pequena : Tema.Normal;
    }

    int IndicePantalla()
    {
        if (op.Pantalla == "*") return 0;
        Screen[] todas = Screen.AllScreens;
        for (int i = 0; i < todas.Length; i++)
            if (todas[i].DeviceName == op.Pantalla || (op.Pantalla == "" && todas[i].Primary)) return i + 1;
        for (int i = 0; i < todas.Length; i++) if (todas[i].Primary) return i + 1; // monitor guardado no conectado
        return 1;
    }

    void PantallasCambiaron(object sender, EventArgs e)
    {
        if (!IsHandleCreated) return;
        BeginInvoke(new MethodInvoker(delegate { LlenarPantallas(); Sincronizar(); }));
    }

    // ---------- Estado ----------

    void ElegirOtroColor()
    {
        using (ColorDialog cd = new ColorDialog())
        {
            cd.FullOpen = true;
            cd.Color = op.ColorPersonal.IsEmpty ? Color.DeepSkyBlue : op.ColorPersonal;
            if (cd.ShowDialog(this) != DialogResult.OK) return;
            op.ColorPersonal = cd.Color;
            op.ColorIdx = -1;
            Aplicar();
        }
    }

    void Aplicar()
    {
        if (cargando) return;
        overlay.AplicarCambios();
        Sincronizar();
        vista.Invalidate();
    }

    // Pone cada control con el valor actual de la configuración.
    void Sincronizar()
    {
        bool antes = cargando;
        cargando = true;
        sincronizando = true;

        foreach (TarjetaDiseno t in tarjetas) { t.Seleccionada = t.Diseno == op.Diseno; t.Invalidate(); }
        for (int i = 0; i < muestras.Count; i++) { muestras[i].Seleccionada = op.ColorIdx == i; muestras[i].Invalidate(); }
        muestraOtro.Color = op.ColorPersonal;
        muestraOtro.Seleccionada = op.ColorIdx < 0;
        muestraOtro.Invalidate();

        ch12.Marcado = op.Formato12;
        chSeg.Marcado = op.Segundos;
        chFecha.Marcado = op.Fecha;
        chCompleta.Marcado = op.PantallaCompleta;
        dTam.Valor = (int)Math.Round(op.Escala * 100);
        dTam.Enabled = !op.PantallaCompleta;
        lTam.Text = op.PantallaCompleta ? "—" : dTam.Valor + " %";
        dOpa.Valor = (int)Math.Round(op.Opacidad * 100);
        lOpa.Text = dOpa.Valor + " %";
        sVertical.Indice = op.PosV;
        sHorizontal.Indice = op.PosH;
        sPantalla.Indice = IndicePantalla();
        for (int i = 0; i < marcos.Length; i++) if (marcos[i] != null) marcos[i].Caja.Valor = op.Atajos[i];
        lAyuda.Text = Acciones.Resumen(op.Atajos);
        chSonido.Marcado = op.SonidoAlarma;

        sincronizando = false;
        cargando = antes;
    }

    void Guardar()
    {
        if (!ValidarAtajos())
        {
            sincronizando = true;
            pestanas.Indice = PAG_ATAJOS;
            sincronizando = false;
            pagApariencia.Visible = pagAlarmas.Visible = pagCrono.Visible = false;
            pagAtajos.Visible = true;
            return;
        }
        try
        {
            op.IniciarConWindows = iInicio.Marcado; // se recuerda en config.ini
            op.Guardar();
            Inicio.Establecer(iInicio.Marcado);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo guardar la configuración:\n" + ex.Message, Program.NOMBRE, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        guardado = true;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        refresco.Stop();
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= PantallasCambiaron;
        overlay.CalendarioActualizado -= CalendarioActualizado;
        if (!guardado)
        {
            op.CopiarDe(original); // Cancelar: se deshacen los cambios que se estaban probando
            overlay.PodarEventos(); // sin eventos de calendarios conectados y luego cancelados
        }
        overlay.AtajosPausados = false;
        overlay.AplicarCambios();
        base.OnFormClosing(e);
    }
}
