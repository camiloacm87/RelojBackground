#!/bin/bash
# Compila "RelojBackground.app" para Mac con chip Apple (M1/M2/M3).
# Requiere las herramientas de línea de comandos de Xcode:  xcode-select --install
set -e
cd "$(dirname "$0")"

APP="RelojBackground.app"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

ARQ="arm64"
if [ "$1" = "--universal" ]; then ARQ="arm64 x86_64"; fi # también para Mac con Intel

BINARIOS=()
for A in $ARQ; do
  echo "Compilando para $A…"
  swiftc -O -parse-as-library -target "$A-apple-macos11.0" \
    -framework Cocoa -framework SwiftUI -framework Carbon -framework ServiceManagement \
    -o "RelojBackground-$A" RelojBackground.swift
  BINARIOS+=("RelojBackground-$A")
done
lipo -create "${BINARIOS[@]}" -output "$APP/Contents/MacOS/RelojBackground"
rm -f "${BINARIOS[@]}"

cp Info.plist "$APP/Contents/Info.plist"
# Icono de la app (Finder, Launchpad, Aplicaciones) a partir de las imágenes de Icono.iconset
iconutil -c icns Icono.iconset -o "$APP/Contents/Resources/RelojBackground.icns"

# Firma local (ad hoc): necesaria para que macOS abra la app en Apple Silicon.
codesign --force --deep --sign - "$APP"

echo "Listo: $APP"
