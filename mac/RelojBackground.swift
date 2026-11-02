// RelojBackground para macOS (Apple Silicon M1/M2/M3 e Intel, macOS 11 o superior).
// Muestra un reloj en vivo siempre encima de las demás ventanas, sin capturar el ratón ni el teclado,
// con alarmas (un despertador animado que suena) y un cronómetro que aparece debajo del reloj.
// Se controla con atajos de teclado globales, el icono de la barra de menús y una ventana de configuración.

import Cocoa
import SwiftUI
import Combine
import Carbon.HIToolbox
import ServiceManagement

let nombreApp = "RelojBackground"

// MARK: - Atajos de teclado

// Una combinación: modificadores Carbon (⌃ ⌥ ⇧ ⌘) + código de tecla. nombre vacío = sin atajo.
struct Atajo: Codable, Equatable {
    var keyCode: UInt32 = 0
    var mods: UInt32 = 0
    var nombre: String = ""

    var vacio: Bool { nombre.isEmpty }

    var texto: String {
        if vacio { return "" }
        var s = ""
        if mods & UInt32(controlKey) != 0 { s += "⌃" }
        if mods & UInt32(optionKey) != 0 { s += "⌥" }
        if mods & UInt32(shiftKey) != 0 { s += "⇧" }
        if mods & UInt32(cmdKey) != 0 { s += "⌘" }
        return s + nombre
    }

    func igual(_ o: Atajo) -> Bool { !vacio && !o.vacio && keyCode == o.keyCode && mods == o.mods }

    static func nombreTecla(_ code: Int, _ caracteres: String?) -> String {
        switch code {
        case kVK_Space: return "Espacio"
        case kVK_Return: return "↩"
        case kVK_Tab: return "⇥"
        case kVK_Delete: return "⌫"
        case kVK_ForwardDelete: return "⌦"
        case kVK_Escape: return "Esc"
        case kVK_LeftArrow: return "←"
        case kVK_RightArrow: return "→"
        case kVK_UpArrow: return "↑"
        case kVK_DownArrow: return "↓"
        case kVK_Home: return "Inicio"
        case kVK_End: return "Fin"
        case kVK_PageUp: return "RePág"
        case kVK_PageDown: return "AvPág"
        case kVK_F1: return "F1"
        case kVK_F2: return "F2"
        case kVK_F3: return "F3"
        case kVK_F4: return "F4"
        case kVK_F5: return "F5"
        case kVK_F6: return "F6"
        case kVK_F7: return "F7"
        case kVK_F8: return "F8"
        case kVK_F9: return "F9"
        case kVK_F10: return "F10"
        case kVK_F11: return "F11"
        case kVK_F12: return "F12"
        default: return (caracteres ?? "?").uppercased()
        }
    }

    static let teclasF: Set<Int> = [kVK_F1, kVK_F2, kVK_F3, kVK_F4, kVK_F5, kVK_F6, kVK_F7, kVK_F8,
                                    kVK_F9, kVK_F10, kVK_F11, kVK_F12, kVK_F13, kVK_F14, kVK_F15]
}

enum Accion: Int, CaseIterable {
    case alternar, config, posicion, grande, pequeno, opaco, transparente, salir, crono, cronoReiniciar

    var nombre: String {
        switch self {
        case .alternar: return "Mostrar / ocultar el reloj"
        case .config: return "Abrir la configuración"
        case .posicion: return "Cambiar de posición"
        case .grande: return "Hacer más grande"
        case .pequeno: return "Hacer más pequeño"
        case .opaco: return "Más opaco"
        case .transparente: return "Más transparente"
        case .salir: return "Cerrar la aplicación"
        case .crono: return "Iniciar / pausar el cronómetro"
        case .cronoReiniciar: return "Reiniciar el cronómetro"
        }
    }

    // Por defecto solo ⌃⌥I y ⌃⌥C; el resto sin atajo.
    static func porDefecto() -> [Atajo] {
        var a = [Atajo](repeating: Atajo(), count: allCases.count)
        let ctrlOpt = UInt32(controlKey | optionKey)
        a[Accion.alternar.rawValue] = Atajo(keyCode: UInt32(kVK_ANSI_I), mods: ctrlOpt, nombre: "I")
        a[Accion.config.rawValue] = Atajo(keyCode: UInt32(kVK_ANSI_C), mods: ctrlOpt, nombre: "C")
        return a
    }
}

// MARK: - Configuración

// Una alarma: hora, si está activa y qué días suena.
struct Alarma: Codable, Equatable, Identifiable {
    static let todos = 0, laborables = 1, hoy = 2, rango = 3
    static let modos = ["Todos los días", "Días laborables (lun–vie)", "Solo hoy", "Rango de fechas"]
    static var hoyDia: Date { Calendar.current.startOfDay(for: Date()) }

    var id = UUID()
    var hora = 7
    var minuto = 0
    var activa = true
    var modo = Alarma.todos
    var fecha = Alarma.hoyDia                                                        // "Solo hoy": el día en que se eligió
    var desde = Alarma.hoyDia                                                        // "Rango de fechas" (ambos incluidos)
    var hasta = Calendar.current.date(byAdding: .day, value: 6, to: Alarma.hoyDia)!

    var texto: String { String(format: "%02d:%02d", hora, minuto) }

    // Las de "Solo hoy" se desactivan al sonar; las demás siguen activas.
    var unaSolaVez: Bool { modo == Alarma.hoy }

    // ¿Le toca sonar ese día?
    func tocaEl(_ dia: Date) -> Bool {
        let cal = Calendar.current
        switch modo {
        case Alarma.laborables: return !cal.isDateInWeekend(dia)
        case Alarma.hoy: return cal.isDate(dia, inSameDayAs: fecha)
        case Alarma.rango: return cal.startOfDay(for: dia) >= cal.startOfDay(for: desde) && cal.startOfDay(for: dia) <= cal.startOfDay(for: hasta)
        default: return true
        }
    }

    init() {}

    enum CodingKeys: String, CodingKey { case id, hora, minuto, activa, modo, fecha, desde, hasta, repetir }

    init(from d: Decoder) throws {
        let k = try d.container(keyedBy: CodingKeys.self)
        id = try k.decodeIfPresent(UUID.self, forKey: .id) ?? id
        hora = try k.decodeIfPresent(Int.self, forKey: .hora) ?? hora
        minuto = try k.decodeIfPresent(Int.self, forKey: .minuto) ?? minuto
        activa = try k.decodeIfPresent(Bool.self, forKey: .activa) ?? activa
        if let m = try k.decodeIfPresent(Int.self, forKey: .modo) {
            modo = min(Alarma.rango, max(Alarma.todos, m))
        } else if let r = try k.decodeIfPresent(Bool.self, forKey: .repetir) {
            modo = r ? Alarma.todos : Alarma.hoy // versiones anteriores: "todos los días" sí o no
        }
        fecha = try k.decodeIfPresent(Date.self, forKey: .fecha) ?? fecha
        desde = try k.decodeIfPresent(Date.self, forKey: .desde) ?? desde
        hasta = try k.decodeIfPresent(Date.self, forKey: .hasta) ?? hasta
    }

    func encode(to e: Encoder) throws {
        var k = e.container(keyedBy: CodingKeys.self)
        try k.encode(id, forKey: .id)
        try k.encode(hora, forKey: .hora)
        try k.encode(minuto, forKey: .minuto)
        try k.encode(activa, forKey: .activa)
        try k.encode(modo, forKey: .modo)
        try k.encode(fecha, forKey: .fecha)
        try k.encode(desde, forKey: .desde)
        try k.encode(hasta, forKey: .hasta)
    }
}

struct Config: Codable, Equatable {
    var diseno = 0
    var colorIdx = 0                 // índice de la paleta, o -1 para color personalizado
    var colorPersonal = ""           // "#RRGGBB"
    var formato12 = false
    var segundos = true
    var fecha = true
    var escala: Double = 1           // 0.2 a 5 (20 % a 500 %)
    var opacidad: Double = 1
    var completa = false             // pantalla completa
    var posV = 0                     // 0 arriba, 1 centro, 2 abajo
    var posH = 2                     // 0 izquierda, 1 centro, 2 derecha
    var pantalla = ""                // "" principal, "*" todas, o el número del monitor
    var atajos = Accion.porDefecto()
    var alarmas: [Alarma] = []
    var sonido = true                // la alarma suena además de mostrarse

    init() {}

    // Cada campo es opcional al leer, para que una configuración guardada por una versión anterior
    // (sin alarmas, por ejemplo) se cargue sin perder lo demás.
    init(from d: Decoder) throws {
        let k = try d.container(keyedBy: CodingKeys.self)
        diseno = try k.decodeIfPresent(Int.self, forKey: .diseno) ?? diseno
        colorIdx = try k.decodeIfPresent(Int.self, forKey: .colorIdx) ?? colorIdx
        colorPersonal = try k.decodeIfPresent(String.self, forKey: .colorPersonal) ?? colorPersonal
        formato12 = try k.decodeIfPresent(Bool.self, forKey: .formato12) ?? formato12
        segundos = try k.decodeIfPresent(Bool.self, forKey: .segundos) ?? segundos
        fecha = try k.decodeIfPresent(Bool.self, forKey: .fecha) ?? fecha
        escala = try k.decodeIfPresent(Double.self, forKey: .escala) ?? escala
        opacidad = try k.decodeIfPresent(Double.self, forKey: .opacidad) ?? opacidad
        completa = try k.decodeIfPresent(Bool.self, forKey: .completa) ?? completa
        posV = try k.decodeIfPresent(Int.self, forKey: .posV) ?? posV
        posH = try k.decodeIfPresent(Int.self, forKey: .posH) ?? posH
        pantalla = try k.decodeIfPresent(String.self, forKey: .pantalla) ?? pantalla
        atajos = try k.decodeIfPresent([Atajo].self, forKey: .atajos) ?? atajos
        alarmas = try k.decodeIfPresent([Alarma].self, forKey: .alarmas) ?? alarmas
        sonido = try k.decodeIfPresent(Bool.self, forKey: .sonido) ?? sonido
    }

    mutating func normalizar() {
        escala = min(5, max(0.2, escala))
        opacidad = min(1, max(0.1, opacidad))
        posV = min(2, max(0, posV))
        posH = min(2, max(0, posH))
        if diseno < 0 || diseno >= Disenio.nombres.count { diseno = 0 }
        if colorIdx >= Paleta.nombres.count || (colorIdx < 0 && colorPersonal.isEmpty) { colorIdx = 0 }
        // Acciones nuevas en esta versión: se añaden sin atajo, conservando los ya elegidos.
        while atajos.count < Accion.allCases.count { atajos.append(Atajo()) }
        if atajos.count > Accion.allCases.count { atajos = Array(atajos.prefix(Accion.allCases.count)) }
    }
}

final class Ajustes: ObservableObject {
    static let shared = Ajustes()
    @Published var c: Config
    @Published var atajosFallidos: [String] = []

    private init() {
        if let d = UserDefaults.standard.data(forKey: "config"),
           let v = try? JSONDecoder().decode(Config.self, from: d) {
            c = v
        } else {
            c = Config()
        }
        c.normalizar()
    }

    func guardar() {
        if let d = try? JSONEncoder().encode(c) { UserDefaults.standard.set(d, forKey: "config") }
    }
}

// MARK: - Cronómetro

final class Cronometro {
    static let shared = Cronometro()
    private var inicio: Date?
    private var acumulado: TimeInterval = 0

    var corriendo: Bool { inicio != nil }
    var tiempo: TimeInterval { acumulado + (inicio.map { Date().timeIntervalSince($0) } ?? 0) }
    // Se ve debajo del reloj mientras corre o está en pausa con tiempo; al reiniciarlo desaparece.
    var visible: Bool { corriendo || acumulado > 0 }

    func alternar() {
        if let i = inicio {
            acumulado += Date().timeIntervalSince(i)
            inicio = nil
        } else {
            inicio = Date()
        }
    }

    func reiniciar() {
        inicio = nil
        acumulado = 0
    }

    static func texto(_ t: TimeInterval) -> String {
        let total = Int(t), h = total / 3600, m = (total / 60) % 60, s = total % 60
        let base = h > 0 ? String(format: "%d:%02d:%02d", h, m, s) : String(format: "%02d:%02d", m, s)
        return base + "." + String(Int(t * 10) % 10)
    }
}

// MARK: - Sonido de la alarma

enum Sonido {
    // Dos pitidos cortos y una pausa, generados en memoria (no necesita archivos).
    static func datosAlarma() -> Data {
        let tasa = 22050
        var m: [Int16] = []
        func tono(_ hz: Double, _ segundos: Double) {
            let n = Int(Double(tasa) * segundos), rampa = tasa / 125
            for i in 0..<n {
                let env = min(1, Double(min(i, n - i)) / Double(rampa))
                let t = Double(i) / Double(tasa)
                let v = sin(2 * .pi * hz * t) * 0.8 + sin(4 * .pi * hz * t) * 0.2
                m.append(Int16(v * env * 0.45 * Double(Int16.max)))
            }
        }
        func silencio(_ segundos: Double) { m.append(contentsOf: [Int16](repeating: 0, count: Int(Double(tasa) * segundos))) }
        for _ in 0..<2 { tono(988, 0.11); silencio(0.07) }
        silencio(0.55)

        var d = Data()
        func u32(_ v: UInt32) { d.append(contentsOf: withUnsafeBytes(of: v.littleEndian) { Array($0) }) }
        func u16(_ v: UInt16) { d.append(contentsOf: withUnsafeBytes(of: v.littleEndian) { Array($0) }) }
        let bytes = UInt32(m.count * 2)
        d.append(contentsOf: Array("RIFF".utf8)); u32(36 + bytes)
        d.append(contentsOf: Array("WAVEfmt ".utf8)); u32(16)
        u16(1); u16(1); u32(UInt32(tasa)); u32(UInt32(tasa * 2)); u16(2); u16(16)
        d.append(contentsOf: Array("data".utf8)); u32(bytes)
        for x in m { d.append(contentsOf: withUnsafeBytes(of: x.littleEndian) { Array($0) }) }
        return d
    }
}

// MARK: - Colores

enum Paleta {
    static let nombres = ["Verde agua", "Azul hielo", "Morado", "Coral", "Ámbar", "Rosa", "Rojo", "Verde lima", "Blanco", "Arcoíris"]
    static let arcoiris = 9

    // Principal, secundario y terciario (los usa el diseño "Tres anillos").
    static let hex: [[String]] = [
        ["#5DCAA5", "#85B7EB", "#F0997B"], ["#85B7EB", "#B5D4F4", "#378ADD"], ["#AFA9EC", "#ED93B1", "#7F77DD"],
        ["#F0997B", "#FAC775", "#ED93B1"], ["#EF9F27", "#FAC775", "#F0997B"], ["#ED93B1", "#AFA9EC", "#F4C0D1"],
        ["#E24B4A", "#F09595", "#EF9F27"], ["#97C459", "#C0DD97", "#5DCAA5"], ["#FFFFFF", "#D3D1C7", "#B4B2A9"],
    ]

    static func color(_ h: String) -> NSColor {
        let s = h.trimmingCharacters(in: CharacterSet(charactersIn: "#"))
        let v = UInt32(s, radix: 16) ?? 0xFFFFFF
        return NSColor(srgbRed: CGFloat((v >> 16) & 0xFF) / 255, green: CGFloat((v >> 8) & 0xFF) / 255,
                       blue: CGFloat(v & 0xFF) / 255, alpha: 1)
    }

    static func aHex(_ c: NSColor) -> String {
        let s = c.usingColorSpace(.sRGB) ?? c
        return String(format: "#%02X%02X%02X", Int(s.redComponent * 255), Int(s.greenComponent * 255), Int(s.blueComponent * 255))
    }

    static func hsv(_ h: Double, _ s: Double, _ v: Double) -> NSColor {
        let hue = (h.truncatingRemainder(dividingBy: 360) + 360).truncatingRemainder(dividingBy: 360) / 360
        return NSColor(hue: CGFloat(hue), saturation: CGFloat(s), brightness: CGFloat(v), alpha: 1)
    }

    static func colores(_ c: Config, _ t: Date) -> [NSColor] {
        if c.colorIdx < 0 {
            let a = color(c.colorPersonal)
            let rgb = a.usingColorSpace(.sRGB) ?? a
            let h = Double(rgb.hueComponent) * 360, s = max(0.35, Double(rgb.saturationComponent))
            return [a, a.blended(withFraction: 0.4, of: .white) ?? a, hsv(h + 40, s, 1)]
        }
        if c.colorIdx == arcoiris {
            let k = Calendar.current.dateComponents([.minute, .second, .nanosecond], from: t)
            let h = Double(k.minute! * 6 + k.second!) + Double(k.nanosecond!) / 1e9 // una vuelta de color por hora
            return [hsv(h + 190, 0.6, 1), hsv(h + 250, 0.5, 1), hsv(h + 130, 0.6, 1)]
        }
        return hex[c.colorIdx].map { color($0) }
    }
}

// MARK: - Diseños del reloj

enum Disenio {
    static let nombres = ["Anillo", "Analógico", "Minimalista", "Píldora compacta", "Tres anillos", "Tarjetas"]

    // Tamaño del lienzo de cada diseño a escala 1.
    static func base(_ d: Int) -> CGSize {
        switch d {
        case 2: return CGSize(width: 460, height: 220) // más ancho: los segundos van junto a los minutos
        case 3: return CGSize(width: 380, height: 120)
        case 5: return CGSize(width: 380, height: 210)
        default: return CGSize(width: 280, height: 280)
        }
    }

    // Píldora del cronómetro (debajo del reloj) y despertador de la alarma, a escala 1.
    static let cronoAncho: CGFloat = 210, cronoAlto: CGFloat = 46, cronoSeparacion: CGFloat = 10
    static let despertador = CGSize(width: 240, height: 268)

    // Tamaño del bloque reloj + cronómetro (si está a la vista) a escala 1.
    static func bloque(_ d: Int, cronometro: Bool) -> CGSize {
        let b = base(d)
        if !cronometro { return b }
        return CGSize(width: max(b.width, cronoAncho), height: b.height + cronoSeparacion + cronoAlto)
    }
}

struct Parte {
    var t: String
    var tam: CGFloat
    var peso: NSFont.Weight
    var color: NSColor
}

// Dibuja en coordenadas del lienzo base (y hacia abajo; el llamador aplica la escala).
struct Dibujo {
    let ctx: CGContext
    let c: Config
    let t: Date
    let col: [NSColor]

    static let fondo = NSColor(srgbRed: 12 / 255, green: 16 / 255, blue: 28 / 255, alpha: 140 / 255)
    static let tenue = NSColor(srgbRed: 224 / 255, green: 230 / 255, blue: 245 / 255, alpha: 230 / 255)
    static let blanco = NSColor.white
    static let es = Locale(identifier: "es_ES")

    init(_ ctx: CGContext, _ c: Config, _ t: Date) {
        self.ctx = ctx; self.c = c; self.t = t
        col = Paleta.colores(c, t)
    }

    func dibujar() {
        switch c.diseno {
        case 1: analogico()
        case 2: minimalista()
        case 3: pildora()
        case 4: tresAnillos()
        case 5: tarjetas()
        default: anillo()
        }
    }

    // MARK: Diseños

    func anillo() {
        disco(140, 140, 128, Dibujo.fondo)
        marcas(140, 140, 98, 102, 107)
        arco(140, 140, 118, 7, segundo / 60, col[0], brillo: true)

        var abajo: [Parte] = []
        if c.segundos { abajo.append(Parte(t: fmt("ss"), tam: 20, peso: .regular, color: col[0])) }
        if c.formato12 { abajo.append(Parte(t: ampm, tam: 16, peso: .regular, color: Dibujo.tenue)) }
        let f = c.fecha, s = !abajo.isEmpty
        let y: CGFloat = f && s ? 136 : f ? 150 : s ? 128 : 140

        if f { linea(140, y - 52, [Parte(t: fechaLarga, tam: 15, peso: .regular, color: Dibujo.tenue)]) }
        linea(140, y, [Parte(t: hora, tam: 66, peso: .semibold, color: Dibujo.blanco)])
        if s { linea(140, y + 54, abajo) }
    }

    func analogico() {
        disco(140, 140, 128, Dibujo.fondo)
        ctx.setStrokeColor(col[0].withAlphaComponent(0.7).cgColor)
        ctx.setLineWidth(2)
        ctx.strokeEllipse(in: CGRect(x: 12, y: 12, width: 256, height: 256))
        marcas(140, 140, 108, 112, 118)
        for i in 1...12 {
            let a = Double(i) * .pi / 6
            linea(140 + 82 * CGFloat(sin(a)), 140 - 82 * CGFloat(cos(a)), [Parte(t: "\(i)", tam: 18, peso: .regular, color: Dibujo.blanco)])
        }
        if c.fecha { linea(140, 186, [Parte(t: fechaCorta, tam: 14, peso: .regular, color: Dibujo.tenue)]) }

        let k = componentes
        let seg = segundo, minu = CGFloat(k.minute!) + seg / 60, hr = CGFloat(k.hour! % 12) + minu / 60
        aguja(hr * 30, 58, 8, Dibujo.blanco)
        aguja(minu * 6, 88, 5, Dibujo.blanco)
        if c.segundos { aguja(seg * 6, 100, 2, col[0]) }
        ctx.setFillColor(col[0].cgColor)
        ctx.fillEllipse(in: CGRect(x: 134, y: 134, width: 12, height: 12))
    }

    func minimalista() {
        // Hora y minutos grandes; a su derecha una columna con los segundos arriba (a la altura de la parte
        // superior de las cifras) y "a. m./p. m." abajo (sobre la misma línea base). Debajo, la fecha.
        let h = Parte(t: hora, tam: 120, peso: .light, color: Dibujo.blanco)
        let s: Parte? = c.segundos ? Parte(t: ":" + fmt("ss"), tam: 46, peso: .light, color: Dibujo.blanco) : nil
        let a: Parte? = c.formato12 ? Parte(t: ampm, tam: 46, peso: .light, color: Dibujo.tenue) : nil

        let anchoHora = ancho(h)
        let anchoColumna = max(s.map { ancho($0) } ?? 0, a.map { ancho($0) } ?? 0)
        let hueco: CGFloat = anchoColumna > 0 ? 10 : 0
        let x = 230 - (anchoHora + hueco + anchoColumna) / 2
        let cifras = fuente(h).capHeight
        let base = (c.fecha ? 92 : 110) + cifras / 2 // centro de las cifras en 92 (o 110)

        texto(h, x: x, base: base)
        if let s = s { texto(s, x: x + anchoHora + hueco, base: base - cifras + fuente(s).capHeight) }
        if let a = a { texto(a, x: x + anchoHora + hueco, base: base) }
        if c.fecha { linea(230, 172, [Parte(t: fechaLarga, tam: 22, peso: .regular, color: Dibujo.tenue)]) }
    }

    func pildora() {
        let p = CGPath(roundedRect: CGRect(x: 6, y: 18, width: 368, height: 84), cornerWidth: 42, cornerHeight: 42, transform: nil)
        ctx.addPath(p); ctx.setFillColor(Dibujo.fondo.cgColor); ctx.fillPath()
        ctx.addPath(p); ctx.setStrokeColor(col[0].withAlphaComponent(0.5).cgColor); ctx.setLineWidth(1.5); ctx.strokePath()
        arco(56, 60, 20, 5, segundo / 60, col[0], brillo: false)

        var h = [Parte(t: hora, tam: 44, peso: .semibold, color: Dibujo.blanco)]
        if c.segundos { h.append(Parte(t: ":" + fmt("ss"), tam: 26, peso: .semibold, color: col[0])) }
        if c.formato12 { h.append(Parte(t: ampm, tam: 16, peso: .regular, color: Dibujo.tenue)) }
        linea(c.fecha ? 184 : 214, 60, h)
        if c.fecha { linea(324, 60, [Parte(t: fechaCorta, tam: 20, peso: .regular, color: Dibujo.tenue)]) }
    }

    func tresAnillos() {
        disco(140, 140, 130, Dibujo.fondo)
        let k = componentes
        let seg = segundo, minu = CGFloat(k.minute!) + seg / 60, hr = CGFloat(k.hour! % 12) + minu / 60
        arco(140, 140, 118, 14, hr / 12, col[2], brillo: false)
        arco(140, 140, 98, 14, minu / 60, col[1], brillo: false)
        if c.segundos { arco(140, 140, 78, 14, seg / 60, col[0], brillo: false) }

        var abajo: [Parte] = []
        if c.segundos { abajo.append(Parte(t: fmt("ss"), tam: 18, peso: .regular, color: col[0])) }
        if c.formato12 { abajo.append(Parte(t: ampm, tam: 14, peso: .regular, color: Dibujo.tenue)) }
        let f = c.fecha, s = !abajo.isEmpty
        let y: CGFloat = f && s ? 138 : f ? 146 : s ? 132 : 140

        if f { linea(140, y - 34, [Parte(t: fechaCorta, tam: 15, peso: .regular, color: Dibujo.tenue)]) }
        linea(140, y, [Parte(t: hora, tam: 40, peso: .semibold, color: Dibujo.blanco)])
        if s { linea(140, y + 32, abajo) }
    }

    func tarjetas() {
        tarjeta(22, fmt(c.formato12 ? "hh" : "HH"))
        tarjeta(208, fmt("mm"))

        let ms = componentes.nanosecond! / 1_000_000
        ctx.setFillColor((ms < 500 ? col[0] : NSColor(white: 1, alpha: 0.3)).cgColor)
        ctx.fillEllipse(in: CGRect(x: 185, y: 73, width: 10, height: 10))
        ctx.fillEllipse(in: CGRect(x: 185, y: 109, width: 10, height: 10))

        if c.segundos {
            ctx.setFillColor(NSColor(white: 1, alpha: 0.2).cgColor)
            ctx.fill(CGRect(x: 140, y: 178, width: 100, height: 4))
            ctx.setFillColor(col[0].cgColor)
            ctx.fill(CGRect(x: 140, y: 178, width: 100 * segundo / 60, height: 4))
        }

        var abajo: [Parte] = []
        if c.fecha { abajo.append(Parte(t: fechaLarga, tam: 15, peso: .regular, color: Dibujo.tenue)) }
        if c.formato12 { abajo.append(Parte(t: ampm, tam: 15, peso: .semibold, color: col[0])) }
        if !abajo.isEmpty { linea(190, c.segundos ? 198 : 188, abajo) }
    }

    // MARK: Cronómetro

    // Píldora con el tiempo del cronómetro (lienzo de cronoAncho x cronoAlto).
    func cronometro(_ tiempo: TimeInterval, corriendo: Bool) {
        let w = Disenio.cronoAncho, h = Disenio.cronoAlto
        let p = CGPath(roundedRect: CGRect(x: 1, y: 1, width: w - 2, height: h - 2), cornerWidth: (h - 2) / 2, cornerHeight: (h - 2) / 2, transform: nil)
        ctx.addPath(p); ctx.setFillColor(Dibujo.fondo.cgColor); ctx.fillPath()
        ctx.addPath(p); ctx.setStrokeColor(col[0].withAlphaComponent(0.5).cgColor); ctx.setLineWidth(1.5); ctx.strokePath()

        // Icono: esfera de cronómetro con su botón y una aguja que gira con los segundos
        let cx: CGFloat = 26, cy = h / 2 + 2, r: CGFloat = 11
        ctx.setStrokeColor((corriendo ? col[0] : Dibujo.tenue).cgColor)
        ctx.setLineWidth(2.4)
        ctx.setLineCap(.round)
        ctx.strokeEllipse(in: CGRect(x: cx - r, y: cy - r, width: 2 * r, height: 2 * r))
        ctx.strokeLineSegments(between: [CGPoint(x: cx, y: cy - r - 1), CGPoint(x: cx, y: cy - r - 4),
                                         CGPoint(x: cx - 3, y: cy - r - 5), CGPoint(x: cx + 3, y: cy - r - 5)])
        let a = tiempo.truncatingRemainder(dividingBy: 60) / 60 * 2 * .pi
        ctx.strokeLineSegments(between: [CGPoint(x: cx, y: cy), CGPoint(x: cx + (r - 4) * CGFloat(sin(a)), y: cy - (r - 4) * CGFloat(cos(a)))])

        let texto = Cronometro.texto(tiempo)
        let punto = texto.lastIndex(of: ".")!
        linea(120, h / 2, [Parte(t: String(texto[..<punto]), tam: 24, peso: .semibold, color: corriendo ? Dibujo.blanco : Dibujo.tenue),
                           Parte(t: String(texto[punto...]), tam: 16, peso: .semibold, color: col[0])])

        if !corriendo { // en pausa: dos barras a la derecha
            ctx.setFillColor(Dibujo.tenue.cgColor)
            ctx.fill(CGRect(x: w - 30, y: h / 2 - 7, width: 4, height: 14))
            ctx.fill(CGRect(x: w - 23, y: h / 2 - 7, width: 4, height: 14))
        }
    }

    // MARK: Alarma

    // Despertador que se sacude (lienzo de 240 x 268). angulo en grados · pulso 0..1 para las ondas.
    func despertador(angulo: CGFloat, pulso: CGFloat, hora textoHora: String) {
        let ac = col[0]
        let cx: CGFloat = 120, cy: CGFloat = 116
        let rad = CGFloat.pi / 180

        // Ondas de sonido a los lados (no giran con el despertador)
        ctx.setLineCap(.round)
        ctx.setLineWidth(5)
        for i in 0..<2 {
            let rr = 90 + CGFloat(i) * 15 + pulso * 8
            let alfa = max(0, 0.78 * (1 - pulso * 0.7) / CGFloat(i + 1))
            ctx.setStrokeColor(ac.withAlphaComponent(alfa).cgColor)
            ctx.beginPath(); ctx.addArc(center: CGPoint(x: cx, y: cy), radius: rr, startAngle: 160 * rad, endAngle: 200 * rad, clockwise: false); ctx.strokePath()
            ctx.beginPath(); ctx.addArc(center: CGPoint(x: cx, y: cy), radius: rr, startAngle: -20 * rad, endAngle: 20 * rad, clockwise: false); ctx.strokePath()
        }

        ctx.saveGState()
        ctx.translateBy(x: cx, y: cy)
        ctx.rotate(by: angulo * rad)
        ctx.translateBy(x: -cx, y: -cy)

        // Patas
        for (ancho, color) in [(CGFloat(12), NSColor(white: 0, alpha: 0.47)), (CGFloat(8), Dibujo.tenue)] {
            ctx.setStrokeColor(color.cgColor)
            ctx.setLineWidth(ancho)
            ctx.strokeLineSegments(between: [CGPoint(x: cx - 40, y: cy + 52), CGPoint(x: cx - 54, y: cy + 72),
                                             CGPoint(x: cx + 40, y: cy + 52), CGPoint(x: cx + 54, y: cy + 72)])
        }

        // Martillo y campanas
        ctx.setStrokeColor(Dibujo.tenue.cgColor)
        ctx.setLineWidth(5)
        ctx.strokeLineSegments(between: [CGPoint(x: cx, y: cy - 64), CGPoint(x: cx, y: cy - 80)])
        ctx.setFillColor(Dibujo.tenue.cgColor)
        ctx.fillEllipse(in: CGRect(x: cx - 7, y: cy - 92, width: 14, height: 14))
        for (centro, inicio) in [(CGPoint(x: cx - 48, y: cy - 62), CGFloat(135)), (CGPoint(x: cx + 48, y: cy - 62), CGFloat(225))] {
            ctx.beginPath()
            ctx.addArc(center: centro, radius: 28, startAngle: inicio * rad, endAngle: (inicio + 180) * rad, clockwise: false)
            ctx.closePath()
            ctx.setFillColor(ac.cgColor)
            ctx.setStrokeColor(NSColor(white: 0, alpha: 0.47).cgColor)
            ctx.setLineWidth(3)
            ctx.drawPath(using: .fillStroke)
        }

        // Cuerpo y esfera
        ctx.setFillColor(NSColor(white: 0, alpha: 0.47).cgColor)
        ctx.fillEllipse(in: CGRect(x: cx - 70, y: cy - 66, width: 140, height: 140))
        ctx.setFillColor(NSColor(srgbRed: 246 / 255, green: 246 / 255, blue: 250 / 255, alpha: 1).cgColor)
        ctx.fillEllipse(in: CGRect(x: cx - 64, y: cy - 64, width: 128, height: 128))
        ctx.setStrokeColor(ac.cgColor)
        ctx.setLineWidth(10)
        ctx.strokeEllipse(in: CGRect(x: cx - 64, y: cy - 64, width: 128, height: 128))
        ctx.setStrokeColor(NSColor(srgbRed: 90 / 255, green: 90 / 255, blue: 100 / 255, alpha: 1).cgColor)
        ctx.setLineWidth(3)
        for i in 0..<12 {
            let a = Double(i) * .pi / 6
            let s = CGFloat(sin(a)), co = CGFloat(cos(a))
            ctx.strokeLineSegments(between: [CGPoint(x: cx + 44 * s, y: cy - 44 * co), CGPoint(x: cx + 51 * s, y: cy - 51 * co)])
        }

        // Agujas con la hora actual
        let k = componentes
        let minu = Double(k.minute!) + Double(k.second!) / 60, hr = Double(k.hour! % 12) + minu / 60
        let oscuro = NSColor(srgbRed: 40 / 255, green: 44 / 255, blue: 56 / 255, alpha: 1).cgColor
        ctx.setStrokeColor(oscuro)
        ctx.setLineWidth(7)
        ctx.strokeLineSegments(between: [CGPoint(x: cx, y: cy), CGPoint(x: cx + 28 * CGFloat(sin(hr * 30 * .pi / 180)), y: cy - 28 * CGFloat(cos(hr * 30 * .pi / 180)))])
        ctx.setLineWidth(4.5)
        ctx.strokeLineSegments(between: [CGPoint(x: cx, y: cy), CGPoint(x: cx + 42 * CGFloat(sin(minu * 6 * .pi / 180)), y: cy - 42 * CGFloat(cos(minu * 6 * .pi / 180)))])
        ctx.setFillColor(ac.cgColor)
        ctx.fillEllipse(in: CGRect(x: cx - 6, y: cy - 6, width: 12, height: 12))
        ctx.restoreGState()

        linea(120, 220, [Parte(t: textoHora, tam: 30, peso: .semibold, color: Dibujo.blanco)])
        linea(120, 252, [Parte(t: "Clic para detener", tam: 13, peso: .regular, color: Dibujo.tenue)])
    }

    // MARK: Piezas comunes

    func tarjeta(_ x: CGFloat, _ texto: String) {
        let p = CGPath(roundedRect: CGRect(x: x, y: 20, width: 150, height: 150), cornerWidth: 14, cornerHeight: 14, transform: nil)
        ctx.addPath(p); ctx.setFillColor(NSColor(srgbRed: 20 / 255, green: 22 / 255, blue: 30 / 255, alpha: 0.85).cgColor); ctx.fillPath()
        ctx.addPath(p); ctx.setStrokeColor(col[0].withAlphaComponent(0.45).cgColor); ctx.setLineWidth(1.5); ctx.strokePath()
        linea(x + 75, 95, [Parte(t: texto, tam: 96, peso: .semibold, color: Dibujo.blanco)])
        ctx.setStrokeColor(NSColor(srgbRed: 10 / 255, green: 10 / 255, blue: 14 / 255, alpha: 0.67).cgColor)
        ctx.setLineWidth(3)
        ctx.strokeLineSegments(between: [CGPoint(x: x, y: 95), CGPoint(x: x + 150, y: 95)])
    }

    func disco(_ cx: CGFloat, _ cy: CGFloat, _ r: CGFloat, _ color: NSColor) {
        ctx.setFillColor(color.cgColor)
        ctx.fillEllipse(in: CGRect(x: cx - r, y: cy - r, width: r * 2, height: r * 2))
    }

    func marcas(_ cx: CGFloat, _ cy: CGFloat, _ rHora: CGFloat, _ rMinuto: CGFloat, _ rExterior: CGFloat) {
        for i in 0..<60 {
            let a = Double(i) * .pi / 30
            let esHora = i % 5 == 0
            let r1 = esHora ? rHora : rMinuto
            let s = CGFloat(sin(a)), co = CGFloat(cos(a))
            ctx.setStrokeColor(NSColor(white: 1, alpha: esHora ? 0.67 : 0.27).cgColor)
            ctx.setLineWidth(esHora ? 2.5 : 1.2)
            ctx.strokeLineSegments(between: [CGPoint(x: cx + r1 * s, y: cy - r1 * co), CGPoint(x: cx + rExterior * s, y: cy - rExterior * co)])
        }
    }

    func arco(_ cx: CGFloat, _ cy: CGFloat, _ r: CGFloat, _ grosor: CGFloat, _ fraccion: CGFloat, _ color: NSColor, brillo: Bool) {
        ctx.saveGState()
        ctx.setLineWidth(grosor)
        ctx.setStrokeColor(NSColor(white: 1, alpha: 0.18).cgColor)
        ctx.strokeEllipse(in: CGRect(x: cx - r, y: cy - r, width: r * 2, height: r * 2))

        // Con el eje y hacia abajo, los ángulos crecientes avanzan en el sentido de las agujas del reloj.
        let f = min(max(fraccion, 0.0015), 0.9999)
        let inicio = -CGFloat.pi / 2, fin = inicio + f * 2 * .pi
        ctx.setLineCap(.round)
        ctx.setStrokeColor(color.cgColor)
        ctx.beginPath()
        ctx.addArc(center: CGPoint(x: cx, y: cy), radius: r, startAngle: inicio, endAngle: fin, clockwise: false)
        ctx.strokePath()

        if brillo {
            let p = CGPoint(x: cx + r * cos(fin), y: cy + r * sin(fin))
            let colores = [NSColor(white: 1, alpha: 0.9).cgColor, color.withAlphaComponent(0).cgColor] as CFArray
            if let g = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB), colors: colores, locations: [0, 1]) {
                ctx.drawRadialGradient(g, startCenter: p, startRadius: 0, endCenter: p, endRadius: 14, options: [])
            }
        }
        ctx.restoreGState()
    }

    func aguja(_ grados: CGFloat, _ largo: CGFloat, _ grosor: CGFloat, _ color: NSColor) {
        let a = Double(grados) * .pi / 180
        let s = CGFloat(sin(a)), co = CGFloat(cos(a))
        let punta = CGPoint(x: 140 + largo * s, y: 140 - largo * co)
        let cola = CGPoint(x: 140 - 14 * s, y: 140 + 14 * co)
        ctx.setLineCap(.round)
        ctx.setStrokeColor(NSColor(white: 0, alpha: 0.43).cgColor)
        ctx.setLineWidth(grosor + 3)
        ctx.strokeLineSegments(between: [cola, punta])
        ctx.setStrokeColor(color.cgColor)
        ctx.setLineWidth(grosor)
        ctx.strokeLineSegments(between: [cola, punta])
    }

    // Una línea de texto con varias partes (distinto tamaño o color) alineadas por la base,
    // centrada en cx; cy es el centro vertical de las cifras de la parte más grande.
    // Cada parte lleva un contorno oscuro para leerse sobre fondos claros.
    func linea(_ cx: CGFloat, _ cy: CGFloat, _ entrada: [Parte]) {
        let partes = entrada.filter { !$0.t.isEmpty }
        if partes.isEmpty { return }
        let fuentes = partes.map { NSFont.monospacedDigitSystemFont(ofSize: $0.tam, weight: $0.peso) }
        let mayor = fuentes.max { $0.pointSize < $1.pointSize }!
        let base = cy + mayor.capHeight / 2

        var anchos: [CGFloat] = []
        var total: CGFloat = 0
        for (i, p) in partes.enumerated() {
            let w = NSAttributedString(string: p.t, attributes: [.font: fuentes[i]]).size().width
            anchos.append(w)
            total += w + hueco(partes, i)
        }

        var x = cx - total / 2
        for (i, p) in partes.enumerated() {
            x += hueco(partes, i)
            let f = fuentes[i]
            let punto = CGPoint(x: x, y: base - f.ascender)
            let grosor: CGFloat = p.peso == .light ? 3 : 6 // % del tamaño de letra
            NSAttributedString(string: p.t, attributes: [.font: f, .strokeColor: NSColor(white: 0, alpha: 0.55), .strokeWidth: grosor]).draw(at: punto)
            NSAttributedString(string: p.t, attributes: [.font: f, .foregroundColor: p.color]).draw(at: punto)
            x += anchos[i]
        }
    }

    func fuente(_ p: Parte) -> NSFont { NSFont.monospacedDigitSystemFont(ofSize: p.tam, weight: p.peso) }

    func ancho(_ p: Parte) -> CGFloat { NSAttributedString(string: p.t, attributes: [.font: fuente(p)]).size().width }

    // Escribe una parte con su borde izquierdo en x y su línea base en base, con el mismo contorno que linea.
    func texto(_ p: Parte, x: CGFloat, base: CGFloat) {
        let f = fuente(p)
        let punto = CGPoint(x: x, y: base - f.ascender)
        let grosor: CGFloat = p.peso == .light ? 3 : 6
        NSAttributedString(string: p.t, attributes: [.font: f, .strokeColor: NSColor(white: 0, alpha: 0.55), .strokeWidth: grosor]).draw(at: punto)
        NSAttributedString(string: p.t, attributes: [.font: f, .foregroundColor: p.color]).draw(at: punto)
    }

    // Espacio antes de la parte i (las que empiezan con ":" o "." van pegadas a la anterior).
    func hueco(_ p: [Parte], _ i: Int) -> CGFloat {
        if i == 0 { return 0 }
        if p[i].t.hasPrefix(":") || p[i].t.hasPrefix(".") { return p[i].tam * 0.08 }
        return max(p[i].tam, p[i - 1].tam * 0.5) * 0.3
    }

    // MARK: Textos

    var componentes: DateComponents { Calendar.current.dateComponents([.hour, .minute, .second, .nanosecond], from: t) }
    var segundo: CGFloat { let k = componentes; return CGFloat(k.second!) + CGFloat(k.nanosecond!) / 1e9 }

    // Los formateadores se crean una sola vez por formato (se dibuja 10 veces por segundo).
    static var formateadores: [String: DateFormatter] = [:]

    func fmt(_ f: String) -> String {
        if let d = Dibujo.formateadores[f] { return d.string(from: t) }
        let d = DateFormatter()
        d.locale = Dibujo.es
        d.dateFormat = f
        Dibujo.formateadores[f] = d
        return d.string(from: t)
    }

    var hora: String { fmt(c.formato12 ? "h:mm" : "HH:mm") }
    var ampm: String { componentes.hour! < 12 ? "a. m." : "p. m." }

    var fechaLarga: String {
        let s = fmt("EEEE d 'de' MMMM")
        return s.prefix(1).uppercased() + s.dropFirst()
    }

    var fechaCorta: String {
        let dia = fmt("EEEE")
        return dia.prefix(1).uppercased() + dia.dropFirst().prefix(2) + " " + fmt("d")
    }
}

// MARK: - Vistas que dibujan

// Se usa en las ventanas transparentes y, con vistaPrevia = true, en la configuración
// (ahí dibuja también un "escritorio" simulado para ver la transparencia).
final class RelojVista: NSView {
    var vistaPrevia = false
    private var temporizador: Timer?

    override var isFlipped: Bool { true } // y hacia abajo, como en los diseños

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        temporizador?.invalidate()
        temporizador = nil
        guard window != nil else { return }
        // 10 veces por segundo, para que el anillo de segundos avance suave.
        let t = Timer(timeInterval: 0.1, repeats: true) { [weak self] _ in
            guard let self = self, self.window?.isVisible == true else { return }
            self.needsDisplay = true
        }
        RunLoop.main.add(t, forMode: .common)
        temporizador = t
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let ctx = NSGraphicsContext.current?.cgContext else { return }
        let c = Ajustes.shared.c
        let b = Disenio.base(c.diseno)
        let ahora = Date()

        if !vistaPrevia {
            ctx.clear(bounds)
            // Reloj arriba y, si está a la vista, el cronómetro debajo.
            let cr = Cronometro.shared
            let bloque = Disenio.bloque(c.diseno, cronometro: cr.visible)
            let k = bounds.width / bloque.width
            let dibujo = Dibujo(ctx, c, ahora)
            ctx.saveGState()
            ctx.translateBy(x: (bounds.width - b.width * k) / 2, y: 0)
            ctx.scaleBy(x: k, y: k)
            dibujo.dibujar()
            ctx.restoreGState()
            if cr.visible {
                ctx.saveGState()
                ctx.translateBy(x: (bounds.width - Disenio.cronoAncho * k) / 2, y: (b.height + Disenio.cronoSeparacion) * k)
                ctx.scaleBy(x: k, y: k)
                dibujo.cronometro(cr.tiempo, corriendo: cr.corriendo)
                ctx.restoreGState()
            }
            return
        }

        // Arriba: un monitor con el escritorio y el reloj en su tamaño y posición reales.
        let abajoMonitor = dibujarMonitor(ctx, c, ahora)

        // Abajo: el reloj de cerca, para ver los detalles del diseño.
        let y0 = abajoMonitor + 14
        NSAttributedString(string: "De cerca", attributes: [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.secondaryLabelColor])
            .draw(at: CGPoint(x: 2, y: y0))
        let cerca = CGRect(x: 0, y: y0 + 20, width: bounds.width, height: bounds.height - y0 - 20)
        guard cerca.height > 40 else { return }
        ctx.saveGState()
        ctx.addPath(CGPath(roundedRect: cerca, cornerWidth: 10, cornerHeight: 10, transform: nil))
        ctx.clip()
        let degradado = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB),
                                   colors: [NSColor(srgbRed: 58 / 255, green: 66 / 255, blue: 84 / 255, alpha: 1).cgColor,
                                            NSColor(srgbRed: 34 / 255, green: 38 / 255, blue: 48 / 255, alpha: 1).cgColor] as CFArray,
                                   locations: [0, 1])!
        ctx.drawLinearGradient(degradado, start: CGPoint(x: 0, y: cerca.minY), end: CGPoint(x: 0, y: cerca.maxY), options: [])
        let k = min((cerca.width - 24) / b.width, (cerca.height - 24) / b.height)
        dibujarReloj(ctx, c, ahora, origen: CGPoint(x: cerca.midX - b.width * k / 2, y: cerca.midY - b.height * k / 2), k: k)
        ctx.restoreGState()
    }

    // Dibuja el reloj con la opacidad elegida, a escala k y con su esquina superior izquierda en origen.
    private func dibujarReloj(_ ctx: CGContext, _ c: Config, _ ahora: Date, origen: CGPoint, k: CGFloat) {
        ctx.saveGState()
        ctx.setAlpha(CGFloat(c.opacidad))
        ctx.beginTransparencyLayer(auxiliaryInfo: nil)
        ctx.translateBy(x: origen.x, y: origen.y)
        ctx.scaleBy(x: k, y: k)
        Dibujo(ctx, c, ahora).dibujar()
        ctx.endTransparencyLayer()
        ctx.restoreGState()
    }

    // Monitor con barra de menús, Dock, una ventana de ejemplo y el reloj. Devuelve dónde termina su base.
    private func dibujarMonitor(_ ctx: CGContext, _ c: Config, _ ahora: Date) -> CGFloat {
        let s = NSScreen.screens.first(where: { $0.numero == c.pantalla }) ?? NSScreen.screens.first
        let real = s?.frame ?? CGRect(x: 0, y: 0, width: 1512, height: 982)
        let util = s?.visibleFrame ?? real
        let marco: CGFloat = 7, menton: CGFloat = 18, ancho = bounds.width - 2
        let pantalla = CGRect(x: 1 + marco, y: 4 + marco, width: ancho - 2 * marco, height: (ancho - 2 * marco) * real.height / real.width)
        let cuerpo = CGRect(x: 1, y: 4, width: ancho, height: pantalla.height + 2 * marco + menton)
        let cx = bounds.midX, arriba = cuerpo.maxY - 1

        // Pie y base de aluminio
        ctx.setFillColor(NSColor(srgbRed: 168 / 255, green: 170 / 255, blue: 178 / 255, alpha: 1).cgColor)
        ctx.beginPath()
        ctx.move(to: CGPoint(x: cx - 20, y: arriba)); ctx.addLine(to: CGPoint(x: cx + 20, y: arriba))
        ctx.addLine(to: CGPoint(x: cx + 28, y: arriba + 34)); ctx.addLine(to: CGPoint(x: cx - 28, y: arriba + 34))
        ctx.closePath(); ctx.fillPath()
        ctx.addPath(CGPath(roundedRect: CGRect(x: cx - 60, y: arriba + 32, width: 120, height: 8), cornerWidth: 4, cornerHeight: 4, transform: nil))
        ctx.setFillColor(NSColor(srgbRed: 196 / 255, green: 198 / 255, blue: 205 / 255, alpha: 1).cgColor)
        ctx.fillPath()

        // Carcasa: marco negro y mentón de aluminio
        ctx.addPath(CGPath(roundedRect: cuerpo, cornerWidth: 12, cornerHeight: 12, transform: nil))
        ctx.setFillColor(NSColor(srgbRed: 206 / 255, green: 208 / 255, blue: 214 / 255, alpha: 1).cgColor)
        ctx.fillPath()
        ctx.addPath(CGPath(roundedRect: CGRect(x: cuerpo.minX, y: cuerpo.minY, width: cuerpo.width, height: pantalla.height + 2 * marco),
                           cornerWidth: 12, cornerHeight: 12, transform: nil))
        ctx.setFillColor(NSColor(srgbRed: 22 / 255, green: 23 / 255, blue: 26 / 255, alpha: 1).cgColor)
        ctx.fillPath()

        ctx.saveGState()
        ctx.clip(to: pantalla)
        // Fondo de escritorio
        let fondo = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB),
                               colors: [NSColor(srgbRed: 120 / 255, green: 90 / 255, blue: 190 / 255, alpha: 1).cgColor,
                                        NSColor(srgbRed: 30 / 255, green: 60 / 255, blue: 130 / 255, alpha: 1).cgColor,
                                        NSColor(srgbRed: 12 / 255, green: 22 / 255, blue: 52 / 255, alpha: 1).cgColor] as CFArray,
                               locations: [0, 0.5, 1])!
        ctx.drawLinearGradient(fondo, start: CGPoint(x: pantalla.minX, y: pantalla.minY), end: CGPoint(x: pantalla.maxX, y: pantalla.maxY), options: [])

        let k = pantalla.width / real.width // de puntos de la pantalla real a la imagen
        // Barra de menús arriba y Dock abajo (lo que queda fuera del área visible)
        let altoMenu = max(4, (real.maxY - util.maxY) * k)
        ctx.setFillColor(NSColor(white: 1, alpha: 0.35).cgColor)
        ctx.fill(CGRect(x: pantalla.minX, y: pantalla.minY, width: pantalla.width, height: altoMenu))
        let altoDock = (util.minY - real.minY) * k
        if altoDock > 2 {
            let dock = CGRect(x: cx - 50, y: pantalla.maxY - altoDock + 1, width: 100, height: altoDock - 2)
            ctx.addPath(CGPath(roundedRect: dock, cornerWidth: 3, cornerHeight: 3, transform: nil))
            ctx.setFillColor(NSColor(white: 1, alpha: 0.35).cgColor)
            ctx.fillPath()
        }

        // Ventana abierta de ejemplo
        let ventana = CGRect(x: pantalla.minX + pantalla.width * 0.08, y: pantalla.minY + pantalla.height * 0.16,
                             width: pantalla.width * 0.5, height: pantalla.height * 0.55)
        ctx.addPath(CGPath(roundedRect: ventana, cornerWidth: 3, cornerHeight: 3, transform: nil))
        ctx.setFillColor(NSColor(srgbRed: 238 / 255, green: 240 / 255, blue: 244 / 255, alpha: 1).cgColor)
        ctx.fillPath()
        ctx.setFillColor(NSColor(srgbRed: 200 / 255, green: 204 / 255, blue: 212 / 255, alpha: 1).cgColor)
        for i in 0..<4 {
            ctx.fill(CGRect(x: ventana.minX + 6, y: ventana.minY + 12 + CGFloat(i) * 9, width: ventana.width * (i % 2 == 0 ? 0.7 : 0.5), height: 4))
        }

        // El reloj, con el tamaño y la posición que tendrá de verdad (y del área crece hacia arriba en macOS)
        let b = Disenio.base(c.diseno)
        let area = c.completa ? real : util
        let escala = c.completa ? min(area.width / b.width, area.height / b.height) : CGFloat(c.escala)
        let w = b.width * escala, h = b.height * escala, m: CGFloat = c.completa ? 0 : 20
        let x = c.posH == 0 ? area.minX + m : c.posH == 1 ? area.midX - w / 2 : area.maxX - w - m
        let yArriba = c.posV == 0 ? area.maxY - m : c.posV == 1 ? area.midY + h / 2 : area.minY + m + h
        dibujarReloj(ctx, c, ahora, origen: CGPoint(x: pantalla.minX + (x - real.minX) * k, y: pantalla.minY + (real.maxY - yArriba) * k),
                     k: escala * k)
        ctx.restoreGState()

        return arriba + 40
    }
}

// Despertador animado; un clic en él apaga la alarma.
final class VistaAlarma: NSView {
    var alPulsar: (() -> Void)?
    var hora = ""
    let inicio = Date()
    private var temporizador: Timer?

    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func mouseDown(with event: NSEvent) { alPulsar?() }
    override func resetCursorRects() { addCursorRect(bounds, cursor: .pointingHand) }

    override func viewDidMoveToWindow() {
        super.viewDidMoveToWindow()
        temporizador?.invalidate()
        temporizador = nil
        guard window != nil else { return }
        let t = Timer(timeInterval: 1.0 / 30, repeats: true) { [weak self] _ in self?.needsDisplay = true }
        RunLoop.main.add(t, forMode: .common)
        temporizador = t
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let ctx = NSGraphicsContext.current?.cgContext else { return }
        ctx.clear(bounds)
        // Se sacude 1,1 s y descansa 0,5 s, como un despertador de campanas.
        let t = Date().timeIntervalSince(inicio)
        let ciclo = t.truncatingRemainder(dividingBy: 1.6)
        let angulo = ciclo < 1.1 ? 14 * sin(ciclo * 2 * .pi * 7) * sin(ciclo / 1.1 * .pi) : 0
        let pulso = (t * 1.6).truncatingRemainder(dividingBy: 1)
        ctx.saveGState()
        ctx.scaleBy(x: bounds.width / Disenio.despertador.width, y: bounds.height / Disenio.despertador.height)
        Dibujo(ctx, Ajustes.shared.c, Date()).despertador(angulo: CGFloat(angulo), pulso: CGFloat(pulso), hora: hora)
        ctx.restoreGState()
    }
}

// MARK: - Pantallas

extension NSScreen {
    var numero: String {
        let n = deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber
        return n.map { "\($0.uint32Value)" } ?? ""
    }

    var nombre: String {
        if #available(macOS 10.15, *) { return localizedName }
        return "Pantalla"
    }
}

enum Identificador {
    // Muestra un número grande en cada pantalla durante 2 segundos para saber cuál es cuál.
    static func mostrar() {
        for (i, s) in NSScreen.screens.enumerated() {
            let tam = CGSize(width: 260, height: 200)
            let marco = CGRect(x: s.frame.midX - tam.width / 2, y: s.frame.midY - tam.height / 2, width: tam.width, height: tam.height)
            let w = NSWindow(contentRect: marco, styleMask: .borderless, backing: .buffered, defer: false)
            w.isReleasedWhenClosed = false
            w.level = .screenSaver
            w.backgroundColor = NSColor(srgbRed: 20 / 255, green: 22 / 255, blue: 30 / 255, alpha: 0.92)
            w.ignoresMouseEvents = true

            let numero = NSTextField(labelWithString: "\(i + 1)")
            numero.font = NSFont.systemFont(ofSize: 96, weight: .semibold)
            numero.textColor = .white
            numero.alignment = .center
            numero.frame = CGRect(x: 0, y: 50, width: tam.width, height: 130)
            let detalle = NSTextField(labelWithString: s == NSScreen.screens.first ? "\(s.nombre) · principal" : s.nombre)
            detalle.font = NSFont.systemFont(ofSize: 13)
            detalle.textColor = NSColor(white: 0.85, alpha: 1)
            detalle.alignment = .center
            detalle.frame = CGRect(x: 0, y: 16, width: tam.width, height: 22)
            w.contentView?.addSubview(numero)
            w.contentView?.addSubview(detalle)
            w.orderFrontRegardless()
            DispatchQueue.main.asyncAfter(deadline: .now() + 2) { w.close() }
        }
    }
}

// MARK: - Atajos globales (Carbon: no necesitan permiso de accesibilidad)

final class TeclasGlobales {
    static let shared = TeclasGlobales()
    var accion: ((Accion) -> Void)?
    private var refs: [EventHotKeyRef] = []
    private var registrados: [Atajo]?
    private var instalado = false

    // Registra los atajos (solo si cambiaron). Devuelve los que el sistema no aceptó.
    @discardableResult
    func registrar(_ atajos: [Atajo]) -> [String] {
        if registrados == atajos { return Ajustes.shared.atajosFallidos }
        quitar()
        instalar()
        var fallidos: [String] = []
        for (i, a) in atajos.enumerated() where !a.vacio {
            var ref: EventHotKeyRef?
            let id = EventHotKeyID(signature: OSType(0x524C_4F4A), id: UInt32(i + 1)) // 'RLOJ'
            let estado = RegisterEventHotKey(a.keyCode, a.mods, id, GetApplicationEventTarget(), 0, &ref)
            if estado == noErr, let r = ref { refs.append(r) } else { fallidos.append(a.texto) }
        }
        registrados = atajos
        return fallidos
    }

    // Mientras se elige un atajo nuevo se sueltan todos, para que la combinación llegue a la ventana.
    func quitar() {
        for r in refs { UnregisterEventHotKey(r) }
        refs.removeAll()
        registrados = nil
    }

    private func instalar() {
        if instalado { return }
        instalado = true
        var tipo = EventTypeSpec(eventClass: OSType(kEventClassKeyboard), eventKind: UInt32(kEventHotKeyPressed))
        InstallEventHandler(GetApplicationEventTarget(), { _, evento, _ in
            var id = EventHotKeyID()
            GetEventParameter(evento, EventParamName(kEventParamDirectObject), EventParamType(typeEventHotKeyID),
                              nil, MemoryLayout<EventHotKeyID>.size, nil, &id)
            if let a = Accion(rawValue: Int(id.id) - 1) {
                DispatchQueue.main.async { TeclasGlobales.shared.accion?(a) }
            }
            return noErr
        }, 1, &tipo, nil, nil)
    }
}

// Captura la siguiente combinación pulsada en la ventana de configuración.
final class Grabador: ObservableObject {
    static let shared = Grabador()
    @Published var accion: Int?      // la acción a la que se está asignando un atajo
    @Published var aviso = ""
    private var monitor: Any?

    func empezar(_ i: Int) {
        detener()
        TeclasGlobales.shared.quitar()
        accion = i
        aviso = ""
        monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] e in
            self?.tecla(e)
            return nil
        }
    }

    func detener() {
        if let m = monitor { NSEvent.removeMonitor(m) }
        monitor = nil
        accion = nil
        Ajustes.shared.atajosFallidos = TeclasGlobales.shared.registrar(Ajustes.shared.c.atajos)
    }

    private func tecla(_ e: NSEvent) {
        guard let i = accion else { return }
        let f = e.modifierFlags.intersection(.deviceIndependentFlagsMask)
        let code = Int(e.keyCode)
        let sinMods = f.intersection([.control, .option, .command, .shift]).isEmpty

        if code == kVK_Escape && sinMods { detener(); return }
        if (code == kVK_Delete || code == kVK_ForwardDelete) && sinMods {
            Ajustes.shared.c.atajos[i] = Atajo()
            detener()
            return
        }
        if f.intersection([.control, .option, .command]).isEmpty && !Atajo.teclasF.contains(code) {
            aviso = "Usa ⌃, ⌥ o ⌘ junto con otra tecla."
            return
        }

        var mods: UInt32 = 0
        if f.contains(.control) { mods |= UInt32(controlKey) }
        if f.contains(.option) { mods |= UInt32(optionKey) }
        if f.contains(.shift) { mods |= UInt32(shiftKey) }
        if f.contains(.command) { mods |= UInt32(cmdKey) }
        let nuevo = Atajo(keyCode: UInt32(code), mods: mods, nombre: Atajo.nombreTecla(code, e.charactersIgnoringModifiers))

        for (j, otro) in Ajustes.shared.c.atajos.enumerated() where j != i && otro.igual(nuevo) {
            aviso = "\(nuevo.texto) ya se usa en «\(Accion(rawValue: j)!.nombre)»."
            return
        }
        Ajustes.shared.c.atajos[i] = nuevo
        detener()
    }
}

// MARK: - Inicio con el Mac

enum InicioSesion {
    static var disponible: Bool {
        if #available(macOS 13.0, *) { return true }
        return false
    }

    static var activo: Bool {
        if #available(macOS 13.0, *) { return SMAppService.mainApp.status == .enabled }
        return false
    }

    static func establecer(_ activar: Bool) {
        guard #available(macOS 13.0, *) else { return }
        do {
            if activar { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() }
        } catch {
            NSLog("Inicio con el Mac: \(error.localizedDescription)")
        }
    }
}

// MARK: - Ventana de configuración (SwiftUI)

struct VistaPrevia: NSViewRepresentable {
    func makeNSView(context: Context) -> RelojVista {
        let v = RelojVista()
        v.vistaPrevia = true
        return v
    }

    func updateNSView(_ v: RelojVista, context: Context) { v.needsDisplay = true }
}

struct VistaConfig: View {
    @ObservedObject var aj = Ajustes.shared
    @ObservedObject var grab = Grabador.shared
    @State var inicio = InicioSesion.activo
    @State var ahora = Date()
    let pulso = Timer.publish(every: 0.1, on: .main, in: .common).autoconnect()
    var cerrar: (Bool, Bool) -> Void // (guardar, iniciar con el Mac)

    var body: some View {
        HStack(alignment: .top, spacing: 16) {
            VStack(alignment: .leading, spacing: 8) {
                VistaPrevia()
                    .frame(width: 250, height: 420)
                    .cornerRadius(8)
                Text(resumenAtajos).font(.caption).foregroundColor(.secondary)
            }
            VStack(alignment: .leading, spacing: 12) {
                TabView {
                    apariencia.tabItem { Text("Apariencia") }
                    alarmas.tabItem { Text("Alarmas") }
                    cronometro.tabItem { Text("Cronómetro") }
                    atajos.tabItem { Text("Atajos") }
                }
                .frame(width: 450, height: 440)
                HStack {
                    if InicioSesion.disponible { Toggle("Iniciar al encender el Mac", isOn: $inicio) }
                    Spacer()
                    Button("Cancelar") { cerrar(false, inicio) }.keyboardShortcut(.cancelAction)
                    Button("Guardar") { cerrar(true, inicio) }.keyboardShortcut(.defaultAction)
                }
            }
        }
        .padding(16)
        .onReceive(pulso) { ahora = $0 }
    }

    var resumenAtajos: String {
        var s: [String] = []
        let a = aj.c.atajos
        if !a[Accion.alternar.rawValue].vacio { s.append("\(a[Accion.alternar.rawValue].texto) muestra u oculta el reloj") }
        if !a[Accion.config.rawValue].vacio { s.append("\(a[Accion.config.rawValue].texto) abre esta ventana") }
        return s.isEmpty ? "Usa el icono de la barra de menús" : s.joined(separator: "\n")
    }

    // MARK: Pestaña Apariencia

    var apariencia: some View {
        VStack(alignment: .leading, spacing: 10) {
            Picker("Diseño", selection: $aj.c.diseno) {
                ForEach(0..<Disenio.nombres.count, id: \.self) { Text(Disenio.nombres[$0]).tag($0) }
            }

            HStack(spacing: 6) {
                Text("Color")
                ForEach(0..<Paleta.nombres.count, id: \.self) { i in muestra(i) }
                ColorPicker("", selection: colorPersonal).labelsHidden().help("Otro color")
            }

            HStack {
                Toggle("12 horas", isOn: $aj.c.formato12)
                Toggle("Segundos", isOn: $aj.c.segundos)
                Toggle("Fecha", isOn: $aj.c.fecha)
            }

            HStack {
                Text(aj.c.completa ? "Tamaño: pantalla completa" : "Tamaño: \(Int((aj.c.escala * 100).rounded())) %")
                Spacer()
                Toggle("Pantalla completa", isOn: $aj.c.completa)
            }
            Slider(value: $aj.c.escala, in: 0.2...5, step: 0.05).disabled(aj.c.completa)

            Text("Opacidad: \(Int((aj.c.opacidad * 100).rounded())) %")
            Slider(value: $aj.c.opacidad, in: 0.1...1, step: 0.05)

            Picker("Vertical", selection: $aj.c.posV) {
                Text("Arriba").tag(0)
                Text("Centro").tag(1)
                Text("Abajo").tag(2)
            }
            Picker("Horizontal", selection: $aj.c.posH) {
                Text("Izquierda").tag(0)
                Text("Centro").tag(1)
                Text("Derecha").tag(2)
            }
            HStack {
                Picker("Pantalla", selection: $aj.c.pantalla) {
                    Text("Todas las pantallas (\(NSScreen.screens.count))").tag("*")
                    Text("Pantalla principal").tag("")
                    ForEach(Array(NSScreen.screens.enumerated()), id: \.offset) { i, s in
                        Text("Pantalla \(i + 1) · \(s.nombre)").tag(s.numero)
                    }
                }
                Button("Identificar") { Identificador.mostrar() }
            }
            Spacer()
        }
        .padding(12)
    }

    func muestra(_ i: Int) -> some View {
        let sel = aj.c.colorIdx == i
        let relleno: AnyView = i == Paleta.arcoiris
            ? AnyView(Circle().fill(AngularGradient(gradient: Gradient(colors: [.red, .yellow, .green, .blue, .purple, .red]), center: .center)))
            : AnyView(Circle().fill(Color(Paleta.color(Paleta.hex[i][0]))))
        return relleno
            .frame(width: 22, height: 22)
            .overlay(Circle().stroke(sel ? Color.accentColor : Color.gray.opacity(0.5), lineWidth: sel ? 3 : 1))
            .help(Paleta.nombres[i])
            .onTapGesture { aj.c.colorIdx = i }
    }

    var colorPersonal: Binding<Color> {
        Binding(
            get: { Color(aj.c.colorPersonal.isEmpty ? NSColor.systemTeal : Paleta.color(aj.c.colorPersonal)) },
            set: { nuevo in
                aj.c.colorPersonal = Paleta.aHex(NSColor(nuevo))
                aj.c.colorIdx = -1
            })
    }

    // MARK: Pestaña Alarmas

    var alarmas: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Text("Alarmas").font(.headline)
                Spacer()
                Toggle("Sonido", isOn: $aj.c.sonido).help("Además del despertador animado, suena un pitido")
                Button("Probar") {
                    let k = Calendar.current.dateComponents([.hour, .minute], from: Date())
                    Controlador.actual?.sonarAlarma(String(format: "%02d:%02d", k.hour ?? 0, k.minute ?? 0))
                }
                Button("+ Agregar alarma") {
                    var a = Alarma()
                    a.hora = (Calendar.current.component(.hour, from: Date()) + 1) % 24 // la próxima hora en punto
                    aj.c.alarmas.append(a)
                }
                .disabled(aj.c.alarmas.count >= 5)
            }
            if aj.c.alarmas.isEmpty {
                Text("No hay alarmas. Pulsa «Agregar alarma» para crear una.").foregroundColor(.secondary)
            }
            ForEach(aj.c.alarmas) { a in
                filaAlarma(a.id)
                Divider()
            }
            Spacer()
        }
        .padding(12)
    }

    // MARK: Pestaña Cronómetro

    var cronometro: some View {
        VStack(alignment: .leading, spacing: 10) {
            Text("Cronómetro").font(.headline)
            HStack {
                Text(Cronometro.texto(tiempoCrono(ahora)))
                    .font(Font.system(size: 30, weight: .semibold).monospacedDigit())
                    .foregroundColor(Cronometro.shared.corriendo ? .primary : .secondary)
                Spacer()
                Button(Cronometro.shared.corriendo ? "Pausar" : Cronometro.shared.visible ? "Continuar" : "Iniciar") {
                    Controlador.actual?.cronometroAlternar()
                    ahora = Date()
                }
                Button("Reiniciar") {
                    Controlador.actual?.cronometroReiniciar()
                    ahora = Date()
                }
                .disabled(!Cronometro.shared.visible)
            }
            Text("Mientras tiene tiempo, se ve en una píldora debajo del reloj. Al reiniciarlo desaparece. " +
                 "También puedes usarlo desde la barra de menús o con un atajo.")
                .font(.caption).foregroundColor(.secondary)
            Spacer()
        }
        .padding(12)
    }

    // El parámetro hace que el texto se refresque con el pulso de la vista.
    func tiempoCrono(_ momento: Date) -> TimeInterval { Cronometro.shared.tiempo }

    // Hora · qué días suena · interruptor · Eliminar; con "Rango de fechas", debajo van el primer y el último día.
    func filaAlarma(_ id: UUID) -> some View {
        let modo = aj.c.alarmas.first { $0.id == id }?.modo ?? Alarma.todos
        let desde = aj.c.alarmas.first { $0.id == id }?.desde ?? Alarma.hoyDia
        return VStack(alignment: .leading, spacing: 6) {
            HStack {
                DatePicker("", selection: horaAlarma(id), displayedComponents: .hourAndMinute)
                    .labelsHidden()
                    .frame(width: 90)
                Picker("", selection: modoAlarma(id)) {
                    ForEach(0..<Alarma.modos.count, id: \.self) { Text(Alarma.modos[$0]).tag($0) }
                }
                .labelsHidden()
                .frame(width: 190)
                .help("Qué días suena la alarma")
                Spacer()
                Toggle("", isOn: activaAlarma(id))
                    .labelsHidden()
                    .toggleStyle(SwitchToggleStyle())
                    .help("Activar o desactivar esta alarma")
                Button("Eliminar") { aj.c.alarmas.removeAll { $0.id == id } }
            }
            if modo == Alarma.rango {
                HStack {
                    Text("Del")
                    DatePicker("", selection: fechaAlarma(id, \.desde), displayedComponents: .date).labelsHidden()
                    Text("al")
                    DatePicker("", selection: fechaAlarma(id, \.hasta), in: desde..., displayedComponents: .date).labelsHidden()
                }
                .padding(.leading, 98)
            }
        }
    }

    // Al cambiar una alarma vuelve a quedar activa ("Solo hoy" pasa a ser el día de hoy).
    func reactivar(_ i: Int) {
        aj.c.alarmas[i].activa = true
        if aj.c.alarmas[i].modo == Alarma.hoy { aj.c.alarmas[i].fecha = Alarma.hoyDia }
    }

    func modoAlarma(_ id: UUID) -> Binding<Int> {
        Binding(
            get: { aj.c.alarmas.first { $0.id == id }?.modo ?? Alarma.todos },
            set: { m in
                guard let i = aj.c.alarmas.firstIndex(where: { $0.id == id }) else { return }
                aj.c.alarmas[i].modo = m
                if m == Alarma.rango && aj.c.alarmas[i].hasta < Alarma.hoyDia {
                    aj.c.alarmas[i].desde = Alarma.hoyDia
                    aj.c.alarmas[i].hasta = Calendar.current.date(byAdding: .day, value: 6, to: Alarma.hoyDia)!
                }
                reactivar(i)
            })
    }

    func activaAlarma(_ id: UUID) -> Binding<Bool> {
        Binding(
            get: { aj.c.alarmas.first { $0.id == id }?.activa ?? false },
            set: { v in
                guard let i = aj.c.alarmas.firstIndex(where: { $0.id == id }) else { return }
                if v { reactivar(i) } else { aj.c.alarmas[i].activa = false }
            })
    }

    func fechaAlarma(_ id: UUID, _ campo: WritableKeyPath<Alarma, Date>) -> Binding<Date> {
        Binding(
            get: { aj.c.alarmas.first { $0.id == id }?[keyPath: campo] ?? Alarma.hoyDia },
            set: { d in
                guard let i = aj.c.alarmas.firstIndex(where: { $0.id == id }) else { return }
                aj.c.alarmas[i][keyPath: campo] = Calendar.current.startOfDay(for: d)
                if aj.c.alarmas[i].hasta < aj.c.alarmas[i].desde { aj.c.alarmas[i].hasta = aj.c.alarmas[i].desde }
                aj.c.alarmas[i].activa = true
            })
    }

    func horaAlarma(_ id: UUID) -> Binding<Date> {
        Binding(
            get: {
                guard let a = aj.c.alarmas.first(where: { $0.id == id }) else { return Date() }
                return Calendar.current.date(bySettingHour: a.hora, minute: a.minuto, second: 0, of: Date()) ?? Date()
            },
            set: { d in
                guard let i = aj.c.alarmas.firstIndex(where: { $0.id == id }) else { return }
                let k = Calendar.current.dateComponents([.hour, .minute], from: d)
                aj.c.alarmas[i].hora = k.hour ?? 0
                aj.c.alarmas[i].minuto = k.minute ?? 0
                reactivar(i) // al cambiar la hora, la alarma queda activa
            })
    }

    // MARK: Pestaña Atajos

    var atajos: some View {
        VStack(alignment: .leading, spacing: 6) {
            Text("Haz clic en un atajo y pulsa la combinación. ⌫ lo deja sin atajo y Esc cancela.")
                .font(.caption).foregroundColor(.secondary)
            ForEach(Accion.allCases, id: \.rawValue) { a in
                HStack {
                    Text(a.nombre).frame(width: 200, alignment: .leading)
                    Button(textoAtajo(a.rawValue)) { grab.empezar(a.rawValue) }
                        .frame(width: 150)
                    Button("Quitar") { aj.c.atajos[a.rawValue] = Atajo(); grab.detener() }
                }
            }
            if !grab.aviso.isEmpty { Text(grab.aviso).foregroundColor(.red).font(.caption) }
            if !aj.atajosFallidos.isEmpty {
                Text("macOS no aceptó: \(aj.atajosFallidos.joined(separator: ", ")). Elige otra combinación.")
                    .foregroundColor(.red).font(.caption)
            }
            Spacer()
            Button("Restablecer atajos") {
                aj.c.atajos = Accion.porDefecto()
                grab.detener()
            }
        }
        .padding(12)
    }

    func textoAtajo(_ i: Int) -> String {
        if grab.accion == i { return "Pulsa la combinación…" }
        let a = aj.c.atajos[i]
        return a.vacio ? "Sin atajo" : a.texto
    }
}

// MARK: - Aplicación

final class Controlador: NSObject, NSApplicationDelegate, NSWindowDelegate {
    static weak var actual: Controlador?

    var ventanas: [NSWindow] = []
    var visible = true
    var item: NSStatusItem!
    var ventanaConfig: NSWindow?
    var copia: Config?
    var guardado = false
    var cancelables = Set<AnyCancellable>()
    let aj = Ajustes.shared
    var abrirConfigAlIniciar = false
    var arrancarOculto = false

    // Alarmas
    var vigilante: Timer?
    var ventanasAlarma: [NSPanel] = []
    var alarmasDisparadas = Set<String>()
    var sonido: NSSound?
    var inicioAlarma = Date()
    var alarmaSonando: Bool { !ventanasAlarma.isEmpty }

    func applicationDidFinishLaunching(_ n: Notification) {
        Controlador.actual = self
        NSApp.setActivationPolicy(.accessory) // sin icono en el Dock
        visible = !arrancarOculto
        crearMenu()

        TeclasGlobales.shared.accion = { [weak self] a in self?.ejecutar(a) }
        aj.atajosFallidos = TeclasGlobales.shared.registrar(aj.c.atajos)

        // Cada cambio de configuración (desde la ventana o los atajos) se aplica al instante.
        aj.$c.sink { [weak self] _ in
            DispatchQueue.main.async {
                guard let self = self else { return }
                self.actualizarVentanas()
                if Grabador.shared.accion == nil {
                    self.aj.atajosFallidos = TeclasGlobales.shared.registrar(self.aj.c.atajos)
                }
            }
        }.store(in: &cancelables)

        NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification, object: nil, queue: .main) { [weak self] _ in
            self?.actualizarVentanas()
        }

        // Órdenes enviadas por otra copia de la app lanzada desde la terminal.
        DistributedNotificationCenter.default().addObserver(forName: Principal.notificacion, object: nil, queue: .main) { [weak self] n in
            self?.comando(n.object as? String ?? "")
        }

        // Las alarmas se vigilan siempre, aunque el reloj esté oculto.
        let v = Timer(timeInterval: 0.5, repeats: true) { [weak self] _ in self?.vigilarAlarmas() }
        RunLoop.main.add(v, forMode: .common)
        vigilante = v

        actualizarVentanas()
        if abrirConfigAlIniciar { abrirConfig() }
    }

    // Abrir la app otra vez (Finder, Launchpad, Spotlight) abre la configuración.
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        abrirConfig()
        return false
    }

    func comando(_ c: String) {
        switch c {
        case "--mostrar": if !visible { alternar() }
        case "--ocultar": if visible { alternar() }
        case "--config": abrirConfig()
        case "--salir": NSApp.terminate(nil)
        default: ejecutar(.alternar)
        }
    }

    func ejecutar(_ a: Accion) {
        switch a {
        case .alternar: if alarmaSonando { detenerAlarma() } else { alternar() } // si suena la alarma, la apaga
        case .config: abrirConfig()
        case .posicion:
            let n = (aj.c.posV * 3 + aj.c.posH + 1) % 9 // recorre las 9 posiciones
            aj.c.posV = n / 3
            aj.c.posH = n % 3
            guardarAjuste()
        case .grande: aj.c.completa = false; aj.c.escala = min(5, aj.c.escala * 1.1); guardarAjuste()
        case .pequeno: aj.c.completa = false; aj.c.escala = max(0.2, aj.c.escala / 1.1); guardarAjuste()
        case .opaco: aj.c.opacidad = min(1, aj.c.opacidad + 0.1); guardarAjuste()
        case .transparente: aj.c.opacidad = max(0.1, aj.c.opacidad - 0.1); guardarAjuste()
        case .salir: NSApp.terminate(nil)
        case .crono: cronometroAlternar()
        case .cronoReiniciar: cronometroReiniciar()
        }
    }

    // Los cambios hechos con atajos se recuerdan (salvo si la configuración está abierta: ahí decide Guardar/Cancelar).
    func guardarAjuste() {
        if ventanaConfig == nil { aj.guardar() }
    }

    // MARK: Cronómetro

    func cronometroAlternar() {
        Cronometro.shared.alternar()
        if Cronometro.shared.corriendo && !visible { alternar() } // al iniciarlo, el reloj se muestra para verlo
        actualizarVentanas()
    }

    func cronometroReiniciar() {
        Cronometro.shared.reiniciar()
        actualizarVentanas()
    }

    // MARK: Alarmas

    func vigilarAlarmas() {
        let ahora = Date()
        if alarmaSonando && ahora.timeIntervalSince(inicioAlarma) >= 120 { detenerAlarma() } // se apaga sola a los 2 minutos

        let k = Calendar.current.dateComponents([.year, .month, .day, .hour, .minute], from: ahora)
        for (i, a) in aj.c.alarmas.enumerated() where a.activa && a.hora == k.hour && a.minuto == k.minute && a.tocaEl(ahora) {
            let clave = "\(k.year!)-\(k.month!)-\(k.day!) \(a.texto) \(a.id)"
            if alarmasDisparadas.contains(clave) { continue } // ya sonó este minuto
            alarmasDisparadas.insert(clave)
            if a.unaSolaVez {
                aj.c.alarmas[i].activa = false // las de "Solo hoy" se desactivan al sonar
                if ventanaConfig == nil { aj.guardar() }
            }
            sonarAlarma(a.texto)
        }
    }

    // Muestra el despertador animado en cada pantalla del reloj y, si está activado, suena.
    func sonarAlarma(_ hora: String) {
        inicioAlarma = Date()
        if !alarmaSonando {
            for s in pantallasDestino() {
                let tam = Disenio.despertador
                let marco = CGRect(x: (s.visibleFrame.midX - tam.width / 2).rounded(), y: (s.visibleFrame.midY - tam.height / 2).rounded(),
                                   width: tam.width, height: tam.height)
                let p = NSPanel(contentRect: marco, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
                p.isReleasedWhenClosed = false
                p.isOpaque = false
                p.backgroundColor = .clear
                p.hasShadow = false
                p.level = .screenSaver
                p.collectionBehavior = [.canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle]
                p.isFloatingPanel = true
                p.becomesKeyOnlyIfNeeded = true
                let v = VistaAlarma()
                v.hora = hora
                v.alPulsar = { [weak self] in self?.detenerAlarma() }
                p.contentView = v
                p.orderFrontRegardless()
                ventanasAlarma.append(p)
            }
        }
        if aj.c.sonido {
            if sonido == nil {
                sonido = NSSound(data: Sonido.datosAlarma())
                sonido?.loops = true
            }
            if sonido?.isPlaying != true { sonido?.play() }
        }
    }

    func detenerAlarma() {
        for p in ventanasAlarma { p.close() }
        ventanasAlarma.removeAll()
        sonido?.stop()
    }

    // MARK: Ventanas transparentes

    func pantallasDestino() -> [NSScreen] {
        let todas = NSScreen.screens
        if aj.c.pantalla == "*" { return todas }
        if let s = todas.first(where: { $0.numero == aj.c.pantalla }) { return [s] }
        return todas.isEmpty ? [] : [todas[0]] // la principal (también si el monitor elegido no está)
    }

    func nuevaVentana() -> NSWindow {
        let w = NSWindow(contentRect: .zero, styleMask: .borderless, backing: .buffered, defer: false)
        w.isReleasedWhenClosed = false
        w.isOpaque = false
        w.backgroundColor = .clear
        w.hasShadow = false
        w.ignoresMouseEvents = true // los clics pasan a lo que hay debajo
        w.level = .screenSaver      // encima de todo, también de apps a pantalla completa
        w.collectionBehavior = [.canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle]
        w.contentView = RelojVista()
        return w
    }

    func marco(en s: NSScreen) -> CGRect {
        let c = aj.c
        let b = Disenio.bloque(c.diseno, cronometro: Cronometro.shared.visible)
        let area = c.completa ? s.frame : s.visibleFrame // visibleFrame excluye la barra de menús y el Dock
        var tam = CGSize(width: b.width * CGFloat(c.escala), height: b.height * CGFloat(c.escala))
        if c.completa {
            let k = min(area.width / b.width, area.height / b.height)
            tam = CGSize(width: b.width * k, height: b.height * k)
        }
        let m: CGFloat = c.completa ? 0 : 20
        let x = c.posH == 0 ? area.minX + m : c.posH == 1 ? area.midX - tam.width / 2 : area.maxX - tam.width - m
        // En macOS el eje y crece hacia arriba.
        let y = c.posV == 0 ? area.maxY - tam.height - m : c.posV == 1 ? area.midY - tam.height / 2 : area.minY + m
        return CGRect(x: x.rounded(), y: y.rounded(), width: tam.width.rounded(), height: tam.height.rounded())
    }

    func actualizarVentanas() {
        let destino = pantallasDestino()
        while ventanas.count > destino.count { ventanas.removeLast().close() }
        while ventanas.count < destino.count { ventanas.append(nuevaVentana()) }
        for (w, s) in zip(ventanas, destino) {
            w.setFrame(marco(en: s), display: true)
            w.alphaValue = CGFloat(aj.c.opacidad)
            w.contentView?.needsDisplay = true
            if visible { w.orderFrontRegardless() } else { w.orderOut(nil) }
        }
    }

    func alternar() {
        visible.toggle()
        actualizarVentanas()
    }

    // MARK: Barra de menús

    func crearMenu() {
        item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let img = NSImage(systemSymbolName: "clock", accessibilityDescription: nombreApp) {
            img.isTemplate = true
            item.button?.image = img
        } else {
            item.button?.title = "⏰"
        }
        item.button?.toolTip = nombreApp
        let menu = NSMenu()
        menu.delegate = self
        item.menu = menu
    }

    // MARK: Configuración

    func abrirConfig() {
        if !visible { alternar() }
        if let w = ventanaConfig {
            NSApp.activate(ignoringOtherApps: true)
            w.makeKeyAndOrderFront(nil)
            return
        }
        copia = aj.c
        guardado = false
        let vista = VistaConfig(cerrar: { [weak self] guardar, inicio in self?.cerrarConfig(guardar: guardar, inicio: inicio) })
        let w = NSWindow(contentViewController: NSHostingController(rootView: vista))
        w.title = "\(nombreApp) · Configuración"
        w.styleMask = [.titled, .closable]
        w.isReleasedWhenClosed = false
        w.delegate = self
        w.center()
        ventanaConfig = w
        NSApp.activate(ignoringOtherApps: true)
        w.makeKeyAndOrderFront(nil)
    }

    func cerrarConfig(guardar: Bool, inicio: Bool) {
        guardado = guardar
        if guardar {
            aj.guardar()
            if InicioSesion.disponible && inicio != InicioSesion.activo { InicioSesion.establecer(inicio) }
        }
        ventanaConfig?.close()
    }

    func windowWillClose(_ n: Notification) {
        guard (n.object as? NSWindow) === ventanaConfig else { return }
        if Grabador.shared.accion != nil { Grabador.shared.detener() }
        if !guardado, let c = copia { aj.c = c } // Cancelar o cerrar la ventana deshace los cambios
        Grabador.shared.aviso = ""
        ventanaConfig = nil
        copia = nil
    }
}

extension Controlador: NSMenuDelegate {
    // El menú se rehace al abrirlo, para mostrar los atajos y el estado actuales.
    func menuNeedsUpdate(_ menu: NSMenu) {
        menu.removeAllItems()
        func opcion(_ titulo: String, _ accion: Accion?, _ sel: Selector, activa: Bool = true) {
            let a = accion.map { aj.c.atajos[$0.rawValue] } ?? Atajo()
            let i = NSMenuItem(title: a.vacio ? titulo : "\(titulo)    \(a.texto)", action: activa ? sel : nil, keyEquivalent: "")
            i.target = self
            menu.addItem(i)
        }
        if alarmaSonando {
            opcion("Detener la alarma", nil, #selector(menuDetener))
            menu.addItem(.separator())
        }
        opcion(visible ? "Ocultar el reloj" : "Mostrar el reloj", .alternar, #selector(menuAlternar))
        opcion("Configuración…", .config, #selector(menuConfig))
        opcion("Cambiar de posición", .posicion, #selector(menuPosicion))
        menu.addItem(.separator())
        let cr = Cronometro.shared
        opcion(cr.corriendo ? "Pausar cronómetro" : cr.visible ? "Continuar cronómetro" : "Iniciar cronómetro", .crono, #selector(menuCrono))
        opcion("Reiniciar cronómetro", .cronoReiniciar, #selector(menuReiniciar), activa: cr.visible)
        menu.addItem(.separator())
        opcion("Salir", .salir, #selector(menuSalir))
    }

    @objc func menuDetener() { detenerAlarma() }
    @objc func menuAlternar() { ejecutar(.alternar) }
    @objc func menuConfig() { abrirConfig() }
    @objc func menuPosicion() { ejecutar(.posicion) }
    @objc func menuCrono() { cronometroAlternar() }
    @objc func menuReiniciar() { cronometroReiniciar() }
    @objc func menuSalir() { NSApp.terminate(nil) }
}

// MARK: - Punto de entrada

@main
enum Principal {
    static let notificacion = Notification.Name("RelojBackground.comando")

    static func main() {
        let args = Array(CommandLine.arguments.dropFirst())
        let ordenes = ["--alternar", "--mostrar", "--ocultar", "--config", "--salir"]
        let orden = args.first(where: { ordenes.contains($0) }) ?? "--alternar"

        // Si ya hay una copia abierta, se le envía la orden y esta termina.
        let yo = NSRunningApplication.current
        let otras = NSRunningApplication.runningApplications(withBundleIdentifier: Bundle.main.bundleIdentifier ?? "")
            .filter { $0.processIdentifier != yo.processIdentifier }
        if !otras.isEmpty {
            DistributedNotificationCenter.default().postNotificationName(notificacion, object: orden, userInfo: nil, deliverImmediately: true)
            return
        }
        if orden == "--salir" { return }

        let app = NSApplication.shared
        let controlador = Controlador()
        controlador.abrirConfigAlIniciar = orden == "--config"
        controlador.arrancarOculto = args.contains("--oculto") || orden == "--ocultar"
        app.delegate = controlador
        app.run()
    }
}
