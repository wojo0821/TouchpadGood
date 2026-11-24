# TouchpadGood

[English](../README.md) | [한국어](README.ko.md) | [日本語](README.ja.md) | [中文 (简体)](README.zh.md) | **Español** | [Deutsch](README.de.md) | [Français](README.fr.md)

**Controlador de panel táctil de precisión (Precision Touchpad) con latencia ultrabaja para gaming y productividad en Windows 11.**
> *Hecho porque los ajustes predeterminados del panel táctil de Windows eran demasiado incómodos — y sí, también para jugar con el panel táctil.*

Servicio Win32 puro en segundo plano (`TouchpadGood.exe`) + Configuración Fluent Design (`TouchpadGoodSettings.exe`) + Instalador todo en uno (`TouchpadGoodSetup.exe`).

---

## Características principales

- **Arquitectura Win32 pura sin latencia (`TouchpadGood.exe`)**
  - Demonio ligero de `~55 KB` — sin WinForms/GDI+, sin icono de bandeja, 0 hooks `WH_MOUSE_LL` y **0% de CPU en reposo**.
  - `GetRawInputData` Zero-Copy con `/unsafe`, análisis directo `byte*` en `hid.dll` y `SendInput` en pila.
  - Prioridad de proceso `High`, programación **MMCSS `"Games"`**, EcoQoS desactivado y GC `SustainedLowLatency`.
- **Integración nativa con Windows 11**
  - Movimiento/toque de 1 dedo, **desplazamiento nativo en el borde derecho (`SingleFingerPanMode = 2`)** y gestos de 4 dedos permanecen 100% nativos del kernel.
  - Seguimiento lineal 1:1 (desactiva la aceleración del puntero a nivel del sistema).
  - Botón de un clic **"Auto-configurar ajustes de Windows"** para optimizar aceleración, retardo de escritura 0, desplazamiento lateral y toque con 1 dedo.
- **Gaming FPS y gestos multitáctiles personalizables**
  - **Entrada simultánea FPS (Apuntar + Disparar)**:
    - Inicia arrastre derecho con 3 dedos → levanta 1 dedo (2 dedos mantienen el arrastre derecho / apuntado) → toca o mantén un 3.er dedo para clic izquierdo / disparar mientras apuntas.
    - Inicia arrastre izquierdo con 2 dedos → toca o mantén un 3.er dedo para clic derecho / arrastre derecho simultáneo.
  - **Bloqueo al dedo principal de puntería**:
    - Al añadir dedos en movimiento (`1 → 1+1` / `1 → 1+2`), el puntero sigue exclusivamente al dedo original.
    - Al iniciar directamente con 2 o 3 dedos (`0 → 2` / `0 → 3`), el puntero sigue al dedo índice (inferior izquierdo).
  - Atajo global **`Ctrl + Alt + T`** para activar o pausar al instante.
- **Interfaz Fluent Design (7 idiomas y actualización automática)**
  - Sigue el tema claro/oscuro de Windows 11, con barra de desplazamiento y desplegables Fluent personalizados.
  - Cambio de idioma en vivo entre 7 idiomas y **actualizador automático desde GitHub** en un clic.

---

## Gestos predeterminados

| Gesto | Acción predeterminada |
| :--- | :--- |
| **1 dedo mover / tocar** | Nativo de Windows (1:1 lineal, aceleración OFF) |
| **Desplazamiento borde derecho (1 dedo)** | Nativo de Windows 11 (`SingleFingerPanMode = 2`) |
| **Desplazamiento borde inferior (1 dedo)** | Desplazamiento horizontal suave (`WM_MOUSEHWHEEL`, 0 hooks en reposo) |
| **Pellizco 2 dedos en borde der./inf.** | Zoom acercar / alejar (`Ctrl + Wheel`) |
| **Arrastrar con 2 dedos** | Arrastre izquierdo |
| **Toque con 2 dedos (`0 → 2`)** | Clic derecho |
| **Moviendo 1 dedo + 1 toque (`1 → 2`)** | Clic izquierdo (mantener = arrastre izq.) |
| **Arrastrar con 3 dedos** | Arrastre derecho |
| **Toque con 3 dedos (`0 → 3`)** | Clic central |
| **Moviendo 1 dedo + 2 toques (`1 → 3`)** | Clic derecho (mantener = arrastre der.) |
| **3D Der → 2D mantener + 3.er dedo** | Arrastre derecho + clic izquierdo simultáneos |
| **2D Izq + 3.er dedo** | Arrastre izquierdo + clic derecho simultáneos |
| **4 dedos** | Nativo de Windows |

---

## Instalación

### Opción 1: Instalador todo en uno (Recomendado)
1. Descarga **[TouchpadGoodSetup.exe](https://github.com/wojo0821/TouchpadGood/releases/latest)** ([descarga directa](https://github.com/wojo0821/TouchpadGood/raw/main/TouchpadGoodSetup.exe)).
2. Ejecuta `TouchpadGoodSetup.exe` y pulsa **Instalar**.
   - Se instala en `%LOCALAPPDATA%\Programs\TouchpadGood` y se puede desinstalar desde **Configuración de Windows → Aplicaciones** restaurando los ajustes originales de Windows.

### Opción 2: Compilar desde el código fuente (Sin Visual Studio)
```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

---

## Licencia

Licencia MIT — consulta [LICENSE](../LICENSE).
