# RelojBackground para Mac (M1/M2/M3)

Reloj transparente que se ve siempre encima de las demás ventanas (también sobre apps a pantalla
completa) y **no captura el ratón ni el teclado**. Incluye **alarmas** (un despertador animado que
suena) y un **cronómetro** que aparece debajo del reloj. Vive en la **barra de menús** y no ocupa
sitio en el Dock. Requiere macOS 11 Big Sur o superior.

## Instalar desde GitHub (lo más fácil)
Descarga **RelojBackground.pkg** desde https://github.com/camiloacm87/RelojBackground/releases/latest,
haz **clic derecho → Abrir** (la primera vez, porque no está firmado por Apple) y sigue el asistente.
La app queda en Aplicaciones y en Launchpad. También está **RelojBackground.dmg** (arrastrar a Aplicaciones).

Para crear tú mismo el instalador con asistente (.pkg):
```
cd ~/Downloads/RelojBackground-Mac
chmod +x compilar.sh crear-pkg.sh
./crear-pkg.sh
```

## 1. Crear el instalador .dmg (una sola vez)

1. Abre **Terminal** e instala las herramientas gratuitas de Apple si no las tienes:
   ```
   xcode-select --install
   ```
2. Entra en la carpeta descomprimida y crea el instalador:
   ```
   cd ~/Downloads/RelojBackground-Mac
   chmod +x compilar.sh crear-dmg.sh
   ./crear-dmg.sh
   ```
   (Para que funcione también en Mac con Intel: `./crear-dmg.sh --universal`)

   La primera vez macOS puede pedir permiso para que Terminal controle el Finder: sirve para
   colocar los iconos de la ventana del instalador. Si no lo das, el .dmg se crea igual.
3. Se genera **RelojBackground.dmg**. Ábrelo y **arrastra RelojBackground a Aplicaciones**.

Si solo quieres la app, sin el .dmg: `./compilar.sh` crea `RelojBackground.app`.

### Instalarlo en otro Mac
Puedes copiar el .dmg a otro Mac. Como la app no está firmada por Apple, la primera vez macOS dirá que
no puede verificar al desarrollador: abre Aplicaciones, haz **clic derecho en RelojBackground → Abrir**
y confirma. Las siguientes veces se abre normal.

## 2. Uso

| Atajo por defecto | Acción |
|---|---|
| **⌃⌥I** (Control + Opción + I) | Mostrar / ocultar el reloj (y apagar la alarma si está sonando) |
| **⌃⌥C** (Control + Opción + C) | Abrir la configuración |

También puedes usar el **icono de reloj de la barra de menús**, o abrir la app otra vez desde
Launchpad, Spotlight o Aplicaciones para ver la configuración.

## 3. Configuración

**Apariencia:** diseño (Anillo, Analógico, Minimalista, Píldora compacta, Tres anillos o Tarjetas),
color, 12 horas, segundos, fecha, tamaño de 20 % a 500 % o pantalla completa, opacidad, posición
vertical y horizontal, y en qué pantallas se ve (con **Identificar**).

**Alarmas:** hasta 5. Cada una tiene su hora, **cuándo suena** (Todos los días, Días laborables lun–vie,
Solo hoy —suena una vez y se desactiva— o Rango de fechas, con los días Del … al …) y un interruptor para activarla. Cuando suena aparece un **despertador que se sacude**
en el centro de la pantalla y, si **Sonido al sonar** está activado, pita. Se apaga con un **clic en el
despertador**, con ⌃⌥I, desde la barra de menús, o sola a los 2 minutos. **Probar** la hace sonar ahora.

**Cronómetro** (pestaña propia): Iniciar, Pausar, Continuar y Reiniciar. Mientras tiene tiempo se ve una pequeña píldora
debajo del reloj; al reiniciarlo desaparece. También desde la barra de menús o con un atajo propio.

**Atajos:** haz clic en el atajo de una acción y pulsa la combinación. ⌫ lo deja sin atajo, Esc cancela
y **Restablecer atajos** vuelve a dejar solo ⌃⌥I y ⌃⌥C. No hace falta dar permisos de accesibilidad.

**Iniciar al encender el Mac** (macOS 13 o superior). En macOS 11–12, añádela a mano en
Ajustes del Sistema → Usuarios y grupos → Ítems de inicio.

La vista previa y el reloj cambian en vivo. **Guardar** conserva los cambios; **Cancelar** los deshace.

## 4. Desde la Terminal

```
R="/Applications/RelojBackground.app/Contents/MacOS/RelojBackground"
"$R" --config      # abre la configuración
"$R" --alternar    # muestra u oculta (o apaga la alarma)
"$R" --mostrar
"$R" --ocultar
"$R" --salir       # cierra la app
```

## Diferencias con la versión de Windows
- El icono está en la barra de menús en lugar de junto al reloj de Windows.
- Los atajos usan ⌃ ⌥ ⇧ ⌘ en lugar de Ctrl / Alt / Mayús / Win.
- Solo muestra el reloj (la opción de mostrar un GIF o PNG es solo de Windows).
