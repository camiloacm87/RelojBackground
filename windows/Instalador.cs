// Instalador de RelojBackground para Windows (RelojBackground-Setup.exe).
// Instala para el usuario actual, sin permisos de administrador, en %LOCALAPPDATA%\Programs\RelojBackground:
// accesos en el menú Inicio y el escritorio, inicio con Windows y entrada en "Aplicaciones instaladas"
// para poder desinstalarlo. El mismo .exe, con --desinstalar, quita todo.
// La app (RelojBackground.exe) va incrustada como recurso.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

static class Instalador
{
    public const string VERSION = "2.2";
    const string CLAVE_DESINSTALAR = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + Program.NOMBRE;
    const string CLAVE_INICIO = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public static string Carpeta
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", Program.NOMBRE); }
    }

    public static string Exe { get { return Path.Combine(Carpeta, Program.NOMBRE + ".exe"); } }
    static string Desinstalador { get { return Path.Combine(Carpeta, "Desinstalar.exe"); } }
    static string AccesoInicio { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), Program.NOMBRE + ".lnk"); } }
    static string AccesoEscritorio { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Program.NOMBRE + ".lnk"); } }

    public static bool Instalado { get { return File.Exists(Exe); } }

    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        bool desinstalar = false, silencioso = false;
        foreach (string a in args)
        {
            if (a.ToLowerInvariant() == "--desinstalar") desinstalar = true;
            if (a.ToLowerInvariant() == "--silencioso") silencioso = true;
        }
        if (desinstalar) Desinstalar(silencioso);
        else Application.Run(new VentanaInstalador());
    }

    // ---------- Instalar ----------

    public static void Instalar(bool escritorio, bool iniciarConWindows)
    {
        CerrarApp();
        Directory.CreateDirectory(Carpeta);

        // La app, sacada de los recursos de este instalador
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(Program.NOMBRE + ".exe"))
        using (FileStream f = File.Create(Exe))
            s.CopyTo(f);

        // Este mismo instalador queda como desinstalador
        if (!string.Equals(Application.ExecutablePath, Desinstalador, StringComparison.OrdinalIgnoreCase))
            File.Copy(Application.ExecutablePath, Desinstalador, true);

        CrearAcceso(AccesoInicio, Exe, "--config", "Reloj transparente, alarmas y cronómetro");
        if (escritorio) CrearAcceso(AccesoEscritorio, Exe, "--config", "Reloj transparente, alarmas y cronómetro");
        else BorrarArchivo(AccesoEscritorio);

        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CLAVE_INICIO))
        {
            if (iniciarConWindows) k.SetValue(Program.NOMBRE, "\"" + Exe + "\"");
            else k.DeleteValue(Program.NOMBRE, false);
            k.DeleteValue("OverlayReloj", false); // nombre de las primeras versiones
        }

        // Entrada en Configuración → Aplicaciones → Aplicaciones instaladas
        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CLAVE_DESINSTALAR))
        {
            k.SetValue("DisplayName", Program.NOMBRE);
            k.SetValue("DisplayVersion", VERSION);
            k.SetValue("Publisher", "camiloacm87");
            k.SetValue("DisplayIcon", Exe + ",0");
            k.SetValue("InstallLocation", Carpeta);
            k.SetValue("UninstallString", "\"" + Desinstalador + "\" --desinstalar");
            k.SetValue("QuietUninstallString", "\"" + Desinstalador + "\" --desinstalar --silencioso");
            k.SetValue("URLInfoAbout", "https://github.com/camiloacm87/RelojBackground");
            k.SetValue("NoModify", 1, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, Microsoft.Win32.RegistryValueKind.DWord);
            k.SetValue("EstimatedSize", (int)((new FileInfo(Exe).Length + new FileInfo(Desinstalador).Length) / 1024),
                       Microsoft.Win32.RegistryValueKind.DWord);
        }
    }

    // Cierra la app si está abierta (para poder reemplazar o borrar el .exe).
    static void CerrarApp()
    {
        IntPtr h = Win32.FindWindow(null, Program.TITULO);
        if (h != IntPtr.Zero) Win32.PostMessage(h, Program.WM_COMANDO, (IntPtr)Program.CMD_SALIR, IntPtr.Zero);
        for (int i = 0; i < 50; i++)
        {
            if (Process.GetProcessesByName(Program.NOMBRE).Length == 0) return;
            Thread.Sleep(100);
        }
        foreach (Process p in Process.GetProcessesByName(Program.NOMBRE))
            try { p.Kill(); p.WaitForExit(3000); } catch { }
    }

    static void CrearAcceso(string ruta, string destino, string argumentos, string descripcion)
    {
        Type tipo = Type.GetTypeFromProgID("WScript.Shell");
        object shell = Activator.CreateInstance(tipo);
        object acceso = tipo.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { ruta });
        Type ta = acceso.GetType();
        ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, acceso, new object[] { destino });
        ta.InvokeMember("Arguments", BindingFlags.SetProperty, null, acceso, new object[] { argumentos });
        ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, acceso, new object[] { Path.GetDirectoryName(destino) });
        ta.InvokeMember("Description", BindingFlags.SetProperty, null, acceso, new object[] { descripcion });
        ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, acceso, new object[] { destino + ",0" });
        ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, acceso, null);
    }

    static void BorrarArchivo(string ruta)
    {
        try { if (File.Exists(ruta)) File.Delete(ruta); } catch { }
    }

    // ---------- Desinstalar ----------

    static void Desinstalar(bool silencioso)
    {
        if (!silencioso && MessageBox.Show("¿Quieres desinstalar " + Program.NOMBRE + "?\n\nTu configuración y tus alarmas se conservan por si lo vuelves a instalar.",
                                           Program.NOMBRE, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        CerrarApp();
        BorrarArchivo(AccesoInicio);
        BorrarArchivo(AccesoEscritorio);
        using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CLAVE_INICIO))
            k.DeleteValue(Program.NOMBRE, false);
        Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(CLAVE_DESINSTALAR, false);
        BorrarArchivo(Exe);

        // El desinstalador no puede borrarse a sí mismo mientras se ejecuta: lo hace un cmd oculto al terminar.
        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe",
            "/c ping -n 3 127.0.0.1 > nul & rmdir /s /q \"" + Carpeta + "\"");
        psi.CreateNoWindow = true;
        psi.UseShellExecute = false;
        try { Process.Start(psi); } catch { }

        if (!silencioso)
            MessageBox.Show(Program.NOMBRE + " se ha desinstalado.", Program.NOMBRE, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}

// Ventana del asistente de instalación, con el mismo aspecto que la configuración de la app.
class VentanaInstalador : Form
{
    readonly Interruptor iEscritorio = new Interruptor(), iInicio = new Interruptor(), iAbrir = new Interruptor();
    readonly BotonModerno bInstalar, bCancelar = new BotonModerno("Cancelar", false);
    readonly Label lEstado = new Label(), lDetalle = new Label();
    readonly Tarjeta opciones = new Tarjeta();
    bool terminado;

    public VentanaInstalador()
    {
        bool actualizar = Instalador.Instalado;
        Text = "Instalar " + Program.NOMBRE;
        Icon = Iconos.Grande;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Tema.Fondo;
        ForeColor = Tema.Texto;
        Font = Tema.Normal;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(540, 420);

        PictureBox logo = new PictureBox();
        logo.SetBounds(24, 22, 64, 64);
        logo.SizeMode = PictureBoxSizeMode.Zoom;
        using (Stream s = typeof(Instalador).Assembly.GetManifestResourceStream("reloj.ico"))
            if (s != null) using (Icon ic = new Icon(s, 64, 64)) logo.Image = ic.ToBitmap();
        Controls.Add(logo);

        Etiqueta(Program.NOMBRE, 104, 26, 400, 30, Tema.Grande, Tema.Texto);
        Etiqueta("Reloj transparente con alarmas y cronómetro · versión " + Instalador.VERSION, 104, 58, 420, 22, Tema.Normal, Tema.Texto2);

        opciones.AltoFila = 46;
        opciones.SetBounds(24, 108, 492, 46 * 3 + 2);
        Controls.Add(opciones);
        Fila(0, "Crear acceso en el escritorio", iEscritorio);
        Fila(1, "Iniciar con Windows", iInicio);
        Fila(2, "Abrir " + Program.NOMBRE + " al terminar", iAbrir);
        iEscritorio.Marcado = iInicio.Marcado = iAbrir.Marcado = true;

        Etiqueta("Se instala solo para tu usuario, sin permisos de administrador, en:", 24, 262, 492, 20, Tema.Pequena, Tema.Texto2);
        Label ruta = Etiqueta(Instalador.Carpeta, 24, 282, 492, 20, Tema.Pequena, Tema.Texto2);
        ruta.AutoEllipsis = true;
        Etiqueta(lEstado, actualizar ? "Ya está instalado: se actualizará y se conservarán tu configuración y tus alarmas." : "",
                 24, 314, 492, 22, Tema.Normal, Tema.Texto);
        Etiqueta(lDetalle, "", 24, 336, 492, 22, Tema.Pequena, Tema.Texto2);

        bInstalar = new BotonModerno(actualizar ? "Actualizar" : "Instalar", true);
        bInstalar.Fondo = bCancelar.Fondo = Tema.Fondo;
        bInstalar.SetBounds(406, 370, 110, 34);
        bCancelar.SetBounds(286, 370, 110, 34);
        bInstalar.Click += delegate { if (terminado) Close(); else Instalar(); };
        bCancelar.Click += delegate { Close(); };
        Controls.Add(bInstalar);
        Controls.Add(bCancelar);
        AcceptButton = bInstalar;
        CancelButton = bCancelar;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Tema.AplicarVentana(this);
    }

    Label Etiqueta(string texto, int x, int y, int w, int h, Font f, Color c)
    {
        return Etiqueta(new Label(), texto, x, y, w, h, f, c);
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

    void Fila(int i, string texto, Interruptor interruptor)
    {
        Label l = new Label();
        l.Text = texto;
        l.Font = Tema.Normal;
        l.ForeColor = Tema.Texto;
        l.BackColor = Tema.Superficie;
        l.TextAlign = ContentAlignment.MiddleLeft;
        l.SetBounds(16, i * 46 + 1, 360, 44);
        opciones.Controls.Add(l);
        interruptor.Fondo = Tema.Superficie;
        interruptor.Location = new Point(opciones.Width - 16 - interruptor.Width, i * 46 + (46 - interruptor.Height) / 2);
        opciones.Controls.Add(interruptor);
    }

    void Instalar()
    {
        bInstalar.Enabled = bCancelar.Enabled = false;
        lEstado.ForeColor = Tema.Texto;
        lEstado.Text = "Instalando…";
        lDetalle.Text = "";
        Refresh();
        try
        {
            Instalador.Instalar(iEscritorio.Marcado, iInicio.Marcado);
            if (iAbrir.Marcado) Process.Start(Instalador.Exe);
        }
        catch (Exception ex)
        {
            lEstado.ForeColor = Tema.Error;
            lEstado.Text = "No se pudo instalar: " + ex.Message;
            bInstalar.Enabled = bCancelar.Enabled = true;
            return;
        }
        terminado = true;
        lEstado.Text = "✓  " + Program.NOMBRE + " está instalado.";
        lDetalle.Text = "Ctrl+Alt+I muestra u oculta el reloj · Ctrl+Alt+C abre la configuración.";
        bInstalar.Text = "Cerrar";
        bInstalar.Enabled = true;
        bCancelar.Visible = false;
        opciones.Enabled = false;
    }
}
