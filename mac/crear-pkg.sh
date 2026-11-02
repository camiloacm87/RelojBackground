#!/bin/bash
# Crea RelojBackground.pkg: el instalador con asistente de macOS (Continuar → Instalar),
# que copia RelojBackground.app en la carpeta Aplicaciones.
# Uso:  ./crear-pkg.sh             (para Mac con chip Apple)
#       ./crear-pkg.sh --universal (para Mac con chip Apple e Intel)
set -e
cd "$(dirname "$0")"

./compilar.sh "$1"

VERSION="$(/usr/libexec/PlistBuddy -c 'Print CFBundleShortVersionString' Info.plist)"
RAIZ="$(mktemp -d)"
TRABAJO="$(mktemp -d)"
mkdir -p "$RAIZ/Applications"
cp -R "RelojBackground.app" "$RAIZ/Applications/"

# Que se instale siempre en Aplicaciones (y no "actualice" otra copia que haya en otra carpeta).
pkgbuild --analyze --root "$RAIZ" "$TRABAJO/componentes.plist" > /dev/null
/usr/libexec/PlistBuddy -c 'Set :0:BundleIsRelocatable false' "$TRABAJO/componentes.plist"

pkgbuild --root "$RAIZ" --component-plist "$TRABAJO/componentes.plist" \
  --identifier local.relojbackground.pkg --version "$VERSION" --install-location / \
  "$TRABAJO/componente.pkg" > /dev/null

# Paquete final con el asistente de instalación
productbuild --package "$TRABAJO/componente.pkg" "RelojBackground.pkg" > /dev/null

rm -rf "$RAIZ" "$TRABAJO"
echo "Listo: RelojBackground.pkg (versión $VERSION)"
echo "Ábrelo con doble clic y sigue el asistente; la app queda en Aplicaciones."
