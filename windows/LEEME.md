# RelojBackground para Windows

Muestra un **reloj en vivo** siempre encima de las demás ventanas, con fondo transparente.
**No captura el ratón ni el teclado**: los clics pasan a la ventana de abajo y puedes seguir trabajando.
Incluye **alarmas** (un despertador animado que suena) y un **cronómetro** que aparece debajo del reloj.
También puede mostrar una imagen GIF animada o un PNG con transparencia.

## Instalar

**Con el instalador (recomendado):** abre `RelojBackground-Setup.exe` y pulsa **Instalar**.
- Se instala solo para tu usuario, sin permisos de administrador, en `%LOCALAPPDATA%\Programs\RelojBackground`.
- Crea el acceso **RelojBackground** en el menú Inicio y, si lo eliges, en el escritorio; también puede iniciar con Windows.
- Para actualizar, abre el instalador de la versión nueva: conserva tu configuración y tus alarmas.
- Para quitarla: **Configuración → Aplicaciones → Aplicaciones instaladas → RelojBackground → Desinstalar**.

**Sin instalar (portátil):** copia `RelojBackground.exe` a una carpeta fija y ábrelo con doble clic.
Si en esa carpeta hay un `config.ini`, la app guarda ahí su configuración (así puedes llevarla a otra PC);
si no, la guarda en `%LOCALAPPDATA%\RelojBackground`. La primera vez crea el acceso del menú Inicio.

Si tenías la versión anterior ("Overlay"), la nueva trae su configuración y ocupa su lugar en el inicio de Windows.

## Configuración
Ábrela con **Ctrl+Alt+C**, desde el menú Inicio, con `RelojBackground.exe --config` o con doble clic en el icono junto al reloj de Windows.

**Apariencia**
- **Diseño:** Anillo, Analógico, Minimalista, Píldora compacta, Tres anillos o Tarjetas.
- **Color:** 9 colores, Arcoíris (cambia poco a poco) o **+** para elegir cualquier color.
- 12 horas, segundos y fecha.
- **Tamaño** de 20 % a 500 %, o **Pantalla completa**.
- Opacidad, **posición vertical** (arriba, centro, abajo) y **horizontal** (izquierda, centro, derecha).
- **Pantalla:** todas o una concreta (**Identificar** muestra el número de cada monitor).
- Iniciar con Windows.

**Alarmas**
- Hasta 5 alarmas. Cambia la hora con la rueda del ratón, las flechas, escribiendo o con clic arriba/abajo de cada número.
- En la lista de cada alarma eliges **cuándo suena**: **Todos los días**, **Días laborables (lun–vie)**, **Solo hoy**
  (suena una vez y se desactiva) o **Rango de fechas…** (abre un calendario: clic en el primer día y en el último).
  El interruptor la activa o desactiva.
- Cuando suena aparece un **despertador que se sacude** en el centro de la pantalla y, si **Sonido al sonar**
  está activado, pita. Se apaga con un **clic en el despertador**, con Ctrl+Alt+I, desde el icono de la bandeja
  o sola a los 2 minutos. **Probar** la hace sonar ahora.

**Cronómetro** (pestaña propia)
- Iniciar, Pausar, Continuar y Reiniciar. Mientras tiene tiempo se ve una pequeña píldora debajo del reloj;
  al reiniciarlo desaparece. También desde el menú del icono o con un atajo propio.

La vista previa y el reloj cambian en vivo. **Guardar** conserva los cambios y **Cancelar** los deshace.
Se guardan en `config.ini` (junto al .exe o en `%LOCALAPPDATA%\RelojBackground`).

La ventana sigue el **tema claro u oscuro** de Windows y usa tu **color de acento**
(Configuración de Windows → Personalización → Colores).

## Atajos de teclado (globales)
Se cambian en **Configuración → Atajos**: haz clic en la casilla de una acción y pulsa la combinación.
Retroceso o Supr la deja sin atajo, y **Restablecer** vuelve a los de fábrica. Si Windows u otra aplicación ya usa
una combinación, o la repites en dos acciones, la ventana te avisa.

| Acción | Por defecto |
|---|---|
| Mostrar / ocultar el reloj (y apagar la alarma si suena) | Ctrl+Alt+I |
| Abrir la configuración | Ctrl+Alt+C |
| Cambiar de posición, tamaño, opacidad | sin atajo |
| Iniciar / pausar y reiniciar el cronómetro | sin atajo |
| Cerrar la aplicación | sin atajo (usa el menú del icono) |

## Línea de comandos
```
RelojBackground.exe                    (abre el reloj con la configuración guardada)
RelojBackground.exe --config           (abre la configuración)
RelojBackground.exe --oculto           (arranca oculto y espera el atajo)
RelojBackground.exe --pos abajo-centro --completa
RelojBackground.exe "C:\ruta\mi.gif" --escala 0.5 --opacidad 0.8 --pos centro
```
Si ya está abierto, el comando se envía a esa misma instancia:
`--alternar` (por defecto; también apaga la alarma), `--mostrar`, `--ocultar`, `--config`, `--salir`.

## Compilar
Ejecuta `compilar.bat`. Genera `RelojBackground.exe` con el compilador de C# que ya trae Windows; no hay que instalar nada.
El código está en `RelojBackground.cs` (reloj, alarmas, cronómetro, pantallas, atajos) y `Configuracion.cs`
(ventana de configuración). `reloj.ico` es el icono del programa.
