# Checklist de entrega

Reglas de la consigna: repositorio público sin `Library`, `Temp`, `Logs` ni `Build`; página de itch.io que funcione en una ventana privada; video de YouTube visible sin iniciar sesión, con la maniobra completa controlada en vivo; y un documento breve con el diagrama (tipo de joint, eje y límites de cada unión) y la explicación (qué máquina y operación, cómo se controla, cómo se eligieron los drives).

## Ya cubierto en el repositorio

- [x] **Sin archivos generados:** `.gitignore` excluye `Library/`, `Temp/`, `Obj/`, `Build/`, `Builds/`, `Logs/`, `UserSettings/` y `MemoryCaptures/`; `git ls-files` no contiene ninguno de esos directorios. Se versionan los `.meta`.
- [x] **Sin rutas absolutas locales** en los archivos de texto versionados.
- [x] **Abre y corre desde un clon limpio** con Unity 6000.3.14f1, sin pasos manuales: la escena `Assets/Scenes/Main.unity` ya contiene el camión y, en un clon limpio, los tests de EditMode (38) y los de PlayMode (44, con los 3 de la escena guardada) pasaron sin pasos manuales (ver `AUDITORIA.md`).
- [x] **Requisitos del mecanismo** (cubiertos por `TruckSpecTests`): 10 cuerpos, 3 tipos de joint (Revolute, Prismatic, Spherical), 7 drives controlados, 2 pasivos, raíz definida.
- [x] **Entrada y lectura de estado:** teclado (Input System) y sliders sincronizados; telemetría `Target` y `Real` por eje.
- [x] **Interacción con Rigidbodies comunes:** 5 cubos de 8 kg.
- [x] **Documento con diagrama y explicación:** [`DIAGRAMA_Y_EXPLICACION.md`](DIAGRAMA_Y_EXPLICACION.md) (Mermaid) y [`diagrama_articulaciones.svg`](diagrama_articulaciones.svg) (imagen). El documento sale a PDF desde un visor de Markdown con Mermaid, o con el SVG insertado en un procesador de texto.
- [x] **README** con qué simula, versión de Unity, cómo ejecutar, controles, tests y build.
- [x] **Guion del video:** [`GUION_VIDEO.md`](GUION_VIDEO.md).
- [x] **Build WebGL reproducible:** `Tools > Truck > Build WebGL` (ver `README.md`).

## Lo hace el usuario

### 1. Repositorio público en GitHub

- [ ] Crear un repositorio **público** vacío en GitHub (sin README ni `.gitignore` propios, para evitar conflictos).
- [ ] Desde la carpeta del proyecto, con la rama `feature/estilo-industrial` (el estilo industrial sobre `main`) o con `main` si se integra antes (`CODESTYLE.md` habla de `main`; decidir antes de subir):

```text
git remote add origin https://github.com/<usuario>/<repositorio>.git
git push -u origin <rama>
```

- [ ] Abrir la URL en una ventana privada y comprobar que se ve el código sin iniciar sesión y que no aparece `Library`, `Temp`, `Logs` ni `Build`.
- [ ] Completar la URL en el `README.md` (sección *Enlaces*).

### 2. Build WebGL comprimida

La build (`Build/WebGL`) no se versiona; hay que generarla y comprimirla con `index.html` en la **raíz** del `.zip` (no dentro de una subcarpeta).

```text
# 1) Compilar (10 a 15 minutos; cerrar antes el Editor que tenga abierto el proyecto)
"<carpeta de Unity>/Editor/6000.3.14f1/Editor/Unity.exe" -batchmode -quit -projectPath "<carpeta del proyecto>" -buildTarget WebGL -executeMethod Truck.BuildScript.BuildWebGl -logFile build.log
# (o desde el Editor: Tools > Truck > Build WebGL)

# 2) Comprimir el contenido de Build/WebGL (PowerShell o Git Bash; tar de Windows escribe rutas con "/")
cd <carpeta del proyecto>
tar -a -c -f Build/camion-hidrogrua-webgl.zip -C Build/WebGL .
```

Comprobar que el `.zip` tiene `index.html` y la carpeta `Build/` en la raíz. Alternativa en PowerShell: `Compress-Archive -Path "<carpeta del proyecto>/Build/WebGL/*" -DestinationPath "<carpeta del proyecto>/Build/camion-hidrogrua-webgl.zip"`. En Windows PowerShell 5.1 las rutas del `.zip` pueden quedar con barras invertidas; si itch.io no lo carga, usar `tar`.

Prueba local previa (equivale al caso de itch.io, que sirve los `.unityweb` sin cabecera de compresión y usa el *Decompression Fallback*):

```text
cd <carpeta del proyecto>/Build/WebGL
python -m http.server 8765 --bind 127.0.0.1
# abrir http://127.0.0.1:8765
```

### 3. itch.io

- [ ] Crear el proyecto (*Kind of project*: **HTML**).
- [ ] Subir el `.zip` y marcar **This file will be played in the browser**.
- [ ] *Viewport dimensions*: por ejemplo 1024 × 768 (la interfaz se verificó a ese tamaño). Opcional: *Click to launch in fullscreen*.
- [ ] Visibilidad: lo que importa es que la página abra y el juego cargue **en una ventana privada, sin iniciar sesión, para cualquiera que tenga el enlace**. Revisar las opciones de visibilidad de la página en itch.io y comprobarlo en una ventana privada.
- [ ] **Abrir la URL en una ventana privada** y comprobar que carga (unos 20 MB), que se ve el panel y la grúa, y que responden el teclado (todos los pares de teclas) y los sliders.
- [ ] Completar la URL en el `README.md`.

Comprobaciones pendientes en un navegador real (no se pudieron probar con eventos sintéticos; ver `CALIBRACION.md`, sección 9): teclas sostenidas, reinicio con `Retroceso` y `P`, botón derecho para orbitar sin menú contextual, flechas sin desplazar la página, y que el agarre, el volcado (compuerta que abre y cierra) y el reposo de las cargas se comporten como en el Editor. Si algo difiere, ajustar y anotarlo en `CALIBRACION.md` antes de grabar.

### 4. Video en YouTube

- [ ] Grabar siguiendo [`GUION_VIDEO.md`](GUION_VIDEO.md) (2 a 3 minutos, maniobra completa controlada en vivo, con la telemetría visible).
- [ ] Subir con visibilidad **Pública** u **Oculta (no listada)**; **no** *Privada*.
- [ ] Abrir el enlace en una ventana privada, sin sesión, y comprobar que reproduce.
- [ ] Completar la URL en el `README.md`.

### 5. Los cuatro enlaces o documentos a entregar

- [ ] URL del repositorio público de GitHub.
- [ ] URL de la página de itch.io.
- [ ] URL del video de YouTube.
- [ ] Documento breve con el diagrama y la explicación: `docs/DIAGRAMA_Y_EXPLICACION.md` (o su PDF).

### Verificación final antes de entregar

- [ ] Las tres URL abren en una ventana privada.
- [ ] El `README.md` tiene las dos URL reales en lugar de los marcadores *COMPLETAR*.
- [ ] `git status` limpio y el último `git push` hecho.
