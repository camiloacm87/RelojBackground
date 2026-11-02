#!/bin/bash
# Crea el instalador RelojBackground.dmg: al abrirlo aparece la app junto a un acceso a
# Aplicaciones, y basta con arrastrar una sobre la otra.
# Uso:  ./crear-dmg.sh            (para Mac con chip Apple)
#       ./crear-dmg.sh --universal (para Mac con chip Apple e Intel)
set -e
cd "$(dirname "$0")"

./compilar.sh "$1"

VOLUMEN="RelojBackground"
DMG="RelojBackground.dmg"
TMP_DMG="$(mktemp -u /tmp/relojbackground.XXXXXX).dmg"
CARPETA="$(mktemp -d)"

cp -R "RelojBackground.app" "$CARPETA/"
ln -s /Applications "$CARPETA/Aplicaciones"
cp "RelojBackground.app/Contents/Resources/RelojBackground.icns" "$CARPETA/.VolumeIcon.icns"

# Imagen de lectura y escritura para colocar los iconos, y después la versión comprimida final.
hdiutil create -volname "$VOLUMEN" -srcfolder "$CARPETA" -fs HFS+ -format UDRW -ov "$TMP_DMG" > /dev/null
MONTAJE="$(hdiutil attach -readwrite -noverify -noautoopen "$TMP_DMG" | grep -o '/Volumes/.*$' | tail -1)"

# Icono del volumen
if command -v SetFile > /dev/null; then SetFile -a C "$MONTAJE"; fi

# Ventana del instalador: iconos grandes, la app a la izquierda y Aplicaciones a la derecha.
# (Se omite en servidores de compilación como GitHub Actions, donde no hay Finder con el que hablar.)
if [ -z "$CI" ]; then
osascript <<FIN || true
tell application "Finder"
  tell disk "$VOLUMEN"
    open
    set current view of container window to icon view
    set toolbar visible of container window to false
    set statusbar visible of container window to false
    set the bounds of container window to {200, 120, 760, 460}
    set opciones to the icon view options of container window
    set arrangement of opciones to not arranged
    set icon size of opciones to 128
    set position of item "RelojBackground.app" of container window to {150, 160}
    set position of item "Aplicaciones" of container window to {410, 160}
    close
    open
    update without registering applications
    delay 1
    close
  end tell
end tell
FIN
fi

sync
hdiutil detach "$MONTAJE" -quiet || hdiutil detach "$MONTAJE" -force -quiet
rm -f "$DMG"
hdiutil convert "$TMP_DMG" -format UDZO -imagekey zlib-level=9 -o "$DMG" > /dev/null
rm -f "$TMP_DMG"
rm -rf "$CARPETA"

echo "Listo: $DMG"
echo "Ábrelo y arrastra RelojBackground a Aplicaciones."
