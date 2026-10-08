using System.Collections;
using System.IO.Ports;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class DemoCuentacuentos : MonoBehaviour
{
    public enum GameState
    {
        StandbyInicial,
        PlayingCutscene,
        WaitingNFC,
        WaitingPageTurn
    }

    public enum StoryBranch
    {
        Ninguna,
        Teo_Fuego,
        Nina_Hielo
    }

    [Header("Estado Actual del Juego")]
    public GameState currentState = GameState.StandbyInicial;
    public StoryBranch currentBranch = StoryBranch.Ninguna;
    public int currentStep = 0;

    [Header("Referencias de Interfaz (UI)")]
    public TextMeshProUGUI txtEstado;
    public TextMeshProUGUI txtCronometro;
    public TextMeshProUGUI txtIndicacion;
    public Image fondoPantallaUI;
    public Camera mainCamera;

    [Header("Luces Virtuales (Pedestales)")]
    public Image luzP1_Teo;
    public Image luzP2_Nina;
    public Image luzP4_Abanico;
    public Image luzP5_Lentes;
    public Image luzP6_Flauta;
    public Image luzP7_Varita;

    public Color colorLuzEncendida = new Color(1f, 0.9f, 0.2f, 1f);
    public Color colorLuzApagada = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    [Header("Configuración de Puerto Serial (Arduino)")]
    public bool usarPuertoSerial = true;
    public string puertoCOM = "COM3";
    public int baudRate = 9600;
    private SerialPort streamSerial;

    private string ultimoComandoLuces = "";

    [Header("Colores de Reinos")]
    public Color colorStandby = new Color(0.1f, 0.1f, 0.18f);
    public Color colorFuego = new Color(0.85f, 0.25f, 0.15f);
    public Color colorHielo = new Color(0.15f, 0.55f, 0.85f);
    public Color colorTextoNegro = Color.black;

    [Header("Configuración")]
    public float duracionCinematica = 5f;

    private bool canReceiveInput = false;
    private bool teclaPresionadaActiva = false;

    void Awake()
    {
        ConfigurarEstiloTextos();
        InicializarPuertoSerial();
    }

    void Start()
    {
        SetStandbyState();
    }

    void OnDestroy()
    {
        if (streamSerial != null && streamSerial.IsOpen)
        {
            ControlarLucesPedestales(false, false, false, false, false, false, forceSend: true);
            streamSerial.Close();
        }
    }

    void InicializarPuertoSerial()
    {
        if (true)
        {
            try
            {
                streamSerial = new SerialPort(puertoCOM, baudRate);
                streamSerial.ReadTimeout = 50;
                streamSerial.Open();
                Debug.Log("[SERIAL] Puerto abierto correctamente en " + puertoCOM);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SERIAL] No se pudo conectar al puerto " + puertoCOM + ": " + e.Message);
            }
        }
    }

    void EnviarDatoSerial(string mensaje)
    {
        if (streamSerial != null && streamSerial.IsOpen)
        {
            try
            {
                // Forzar salto de línea claro para el Arduino
                streamSerial.Write(mensaje + "\n");
                Debug.Log("[SERIAL OUT ENVIADO] " + mensaje);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SERIAL ERROR] No se pudo enviar por el puerto: " + e.Message);
            }
        }
        else
        {
            Debug.LogWarning("[SERIAL OUT FALLIDO] Puerto serial cerrado o no asignado.");
        }
    }

    void ControlarLucesPedestales(bool p1, bool p2, bool p4, bool p5, bool p6, bool p7, bool forceSend = false)
    {
        // 1. Actualización visual en Canvas/UI de Unity
        if (luzP1_Teo != null) luzP1_Teo.color = p1 ? colorLuzEncendida : colorLuzApagada;
        if (luzP2_Nina != null) luzP2_Nina.color = p2 ? colorLuzEncendida : colorLuzApagada;
        if (luzP4_Abanico != null) luzP4_Abanico.color = p4 ? colorLuzEncendida : colorLuzApagada;
        if (luzP5_Lentes != null) luzP5_Lentes.color = p5 ? colorLuzEncendida : colorLuzApagada;
        if (luzP6_Flauta != null) luzP6_Flauta.color = p6 ? colorLuzEncendida : colorLuzApagada;
        if (luzP7_Varita != null) luzP7_Varita.color = p7 ? colorLuzEncendida : colorLuzApagada;

        // 2. Transmisión a Arduino
        string comando = string.Format("LUCES:{0},{1},{2},{3},{4},{5}",
            p1 ? 1 : 0, p2 ? 1 : 0, p4 ? 1 : 0, p5 ? 1 : 0, p6 ? 1 : 0, p7 ? 1 : 0);

        // Si forzamos el envío o el comando cambió respecto al anterior, lo transmitimos
        if (forceSend || comando != ultimoComandoLuces)
        {
            ultimoComandoLuces = comando;
            EnviarDatoSerial(comando);
        }
    }

    void ActivarVibracionControl(float intensidadBaja, float intensidadAlta, float duracion)
    {
        if (Gamepad.current != null)
        {
            StartCoroutine(RoutineVibracionControl(intensidadBaja, intensidadAlta, duracion));
        }
    }

    IEnumerator RoutineVibracionControl(float baja, float alta, float duracion)
    {
        Gamepad.current.SetMotorSpeeds(baja, alta);
        yield return new WaitForSeconds(duracion);
        Gamepad.current.SetMotorSpeeds(0f, 0f);
    }

    void ConfigurarEstiloTextos()
    {
        if (txtEstado != null) txtEstado.color = colorTextoNegro;
        if (txtCronometro != null) txtCronometro.color = colorTextoNegro;
        if (txtIndicacion != null) txtIndicacion.color = colorTextoNegro;
    }

    void Update()
    {
        // Lectura del puerto serial
        if (streamSerial != null && streamSerial.IsOpen)
        {
            try
            {
                string mensajeSerial = streamSerial.ReadLine();
                if (!string.IsNullOrEmpty(mensajeSerial))
                {
                    ProcesarMensajeArduino(mensajeSerial);
                }
            }
            catch (System.TimeoutException)
            {
                // Timeout normal cuando no hay lecturas entrantes
            }
        }

        // Lectura por teclado
        if (canReceiveInput && Keyboard.current != null)
        {
            HandleInput();
        }
    }

    // =========================================================================
    // PROCESAMIENTO Y IDENTIFICACIÓN DE CÓDIGOS NFC
    // =========================================================================
    private void ProcesarMensajeArduino(string mensaje)
    {
        string comando = mensaje.Trim();

        if (comando.ToLower().StartsWith("ok:")) return;

        Debug.Log($"[SERIAL IN] Cadena recibida: '{comando}'");

        // Limpieza de sufijos y prefijos comunes ("RFID:", "TAG:", etc.)
        string codigoLimpio = comando.ToUpper();
        if (codigoLimpio.StartsWith("RFID:"))
        {
            codigoLimpio = codigoLimpio.Substring(5).Trim();
        }

        Debug.Log($"[NFC DEDUCIDO] Código procesado: '{codigoLimpio}'");

        if (currentState != GameState.WaitingNFC)
        {
            Debug.LogWarning("[SERIAL IN] Se recibió un código pero el flujo actual no está en 'WaitingNFC'.");
            return;
        }

        // Detección flexible por coincidencia parcial o total
        string objetoReconocido = IdentificarObjetoPorCodigo(codigoLimpio);

        // EVALUACIÓN SEGÚN EL PASO ACTUAL DE LA HISTORIA
        if (currentStep == 1)
        {
            if (objetoReconocido == "TEO")
            {
                EjecutarSeleccionPersonaje("P1_TEO", true, false, StoryBranch.Teo_Fuego, colorFuego, "Teo (Fundidora)");
            }
            else if (objetoReconocido == "NINA")
            {
                EjecutarSeleccionPersonaje("P2_NINA", false, true, StoryBranch.Nina_Hielo, colorHielo, "Nina (Laberinto)");
            }
            else
            {
                Debug.LogWarning($"[NFC] Tarjeta recibida ('{codigoLimpio}') no coincide con ningún personaje activo.");
            }
        }
        else if (currentStep == 2)
        {
            if (currentBranch == StoryBranch.Teo_Fuego)
            {
                if (objetoReconocido == "ABANICO")
                {
                    EjecutarSeleccionProp("P4_ABANICO", true, false, false, false, "Prop 4: ABANICO");
                }
                else if (objetoReconocido == "LENTES")
                {
                    EjecutarSeleccionProp("P5_LENTES", false, true, false, false, "Prop 5: LENTES");
                }
                else
                {
                    Debug.LogWarning($"[NFC] Tarjeta '{codigoLimpio}' no es un prop válido para la rama Teo.");
                }
            }
            else if (currentBranch == StoryBranch.Nina_Hielo)
            {
                if (objetoReconocido == "FLAUTA")
                {
                    EjecutarSeleccionProp("P6_FLAUTA", false, false, true, false, "Prop 6: FLAUTA");
                }
                else if (objetoReconocido == "VARITA")
                {
                    EjecutarSeleccionProp("P7_VARITA", false, false, false, true, "Prop 7: VARITA");
                }
                else
                {
                    Debug.LogWarning($"[NFC] Tarjeta '{codigoLimpio}' no es un prop válido para la rama Nina.");
                }
            }
        }
    }

    // Método para asociar cualquier subcadena/código de 6 u 8 dígitos al objeto
    private string IdentificarObjetoPorCodigo(string codigo)
    {
        // TEO (E1767005 / 176700 / E17670)[cite: 8]
        if (codigo.Contains("E1767005") || codigo.Contains("176700") || codigo == "PERRO" || codigo == "A")
            return "TEO";

        // NINA (E10A7105 / 10A710 / E10A71)[cite: 8]
        if (codigo.Contains("E10A7105") || codigo.Contains("10A710") || codigo == "GATO" || codigo == "B")
            return "NINA";

        // ABANICO (56824007 / 568240 / 682400)[cite: 8]
        if (codigo.Contains("56824007") || codigo.Contains("568240") || codigo.Contains("682400"))
            return "ABANICO";

        // LENTES (0E757005 / E75700 / 0E7570)[cite: 8]
        if (codigo.Contains("0E757005") || codigo.Contains("E75700") || codigo.Contains("0E7570"))
            return "LENTES";

        // FLAUTA (77B74107 / 7B7410 / 77B741)[cite: 8]
        if (codigo.Contains("77B74107") || codigo.Contains("7B7410") || codigo.Contains("77B741"))
            return "FLAUTA";

        // VARITA (6C76D605 / C76D60 / 6C76D6)[cite: 8]
        if (codigo.Contains("6C76D605") || codigo.Contains("C76D60") || codigo.Contains("6C76D6"))
            return "VARITA";

        return "UNKNOWN";
    }

    private void EjecutarSeleccionPersonaje(string idObjeto, bool luzP1, bool luzP2, StoryBranch rama, Color colorRama, string nombreSeleccion)
    {
        EnviarDatoSerial("SELECT:" + idObjeto);
        ControlarLucesPedestales(luzP1, luzP2, false, false, false, false, forceSend: true);
        ActivarVibracionControl(0.5f, 0.8f, 0.3f);

        currentBranch = rama;
        CambiarColorFondo(colorRama);
        OnDecisionMade(nombreSeleccion);
    }

    private void EjecutarSeleccionProp(string idObjeto, bool luzP4, bool luzP5, bool luzP6, bool luzP7, string nombreSeleccion)
    {
        EnviarDatoSerial("SELECT:" + idObjeto);
        ControlarLucesPedestales(false, false, luzP4, luzP5, luzP6, luzP7, forceSend: true);
        ActivarVibracionControl(0.7f, 1.0f, 0.3f);

        OnDecisionMade(nombreSeleccion);
    }

    void HandleInput()
    {
        if (currentState == GameState.StandbyInicial)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                StartNextCutscene("Intro: Mercado de Upsilon");
            }
        }
        else if (currentState == GameState.WaitingNFC)
        {
            if (currentStep == 1)
            {
                ProcesarEleccionSostenida(
                    Keyboard.current.digit1Key, Keyboard.current.aKey,
                    "P1_TEO", true, false, StoryBranch.Teo_Fuego, colorFuego, "Teo (Fundidora)"
                );

                ProcesarEleccionSostenida(
                    Keyboard.current.digit2Key, Keyboard.current.bKey,
                    "P2_NINA", false, true, StoryBranch.Nina_Hielo, colorHielo, "Nina (Laberinto)"
                );
            }
            else if (currentStep == 2)
            {
                if (currentBranch == StoryBranch.Teo_Fuego)
                {
                    ProcesarEleccionPropSostenida(Keyboard.current.digit4Key, "P4_ABANICO", true, false, false, false, "Prop 4: ABANICO");
                    ProcesarEleccionPropSostenida(Keyboard.current.digit5Key, "P5_LENTES", false, true, false, false, "Prop 5: LENTES");
                }
                else if (currentBranch == StoryBranch.Nina_Hielo)
                {
                    ProcesarEleccionPropSostenida(Keyboard.current.digit6Key, "P6_FLAUTA", false, false, true, false, "Prop 6: FLAUTA");
                    ProcesarEleccionPropSostenida(Keyboard.current.digit7Key, "P7_VARITA", false, false, false, true, "Prop 7: VARITA");
                }
            }
        }
        else if (currentState == GameState.WaitingPageTurn)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
            {
                AvanceDespuesDePagina();
            }
        }
    }

    void ProcesarEleccionSostenida(
        UnityEngine.InputSystem.Controls.KeyControl tecla1,
        UnityEngine.InputSystem.Controls.KeyControl tecla2,
        string idObjeto,
        bool luzP1, bool luzP2,
        StoryBranch rama,
        Color colorRama,
        string nombreSeleccion)
    {
        bool justoPresionada = (tecla1 != null && tecla1.wasPressedThisFrame) || (tecla2 != null && tecla2.wasPressedThisFrame);
        bool justoSoltada = (tecla1 != null && tecla1.wasReleasedThisFrame) || (tecla2 != null && tecla2.wasReleasedThisFrame);

        if (justoPresionada)
        {
            teclaPresionadaActiva = true;
            EnviarDatoSerial("SELECT:" + idObjeto);
            ControlarLucesPedestales(luzP1, luzP2, false, false, false, false, forceSend: true);
            ActivarVibracionControl(0.5f, 0.8f, 0.3f);

            txtEstado.text = "ESTADO: OBJETO DETECTADO [" + idObjeto + "]";
            txtIndicacion.text = "Mantén presionado para confirmar la selección...";
        }

        if (justoSoltada && teclaPresionadaActiva)
        {
            teclaPresionadaActiva = false;
            currentBranch = rama;
            CambiarColorFondo(colorRama);
            OnDecisionMade(nombreSeleccion);
        }
    }

    void ProcesarEleccionPropSostenida(
        UnityEngine.InputSystem.Controls.KeyControl tecla,
        string idObjeto,
        bool luzP4, bool luzP5, bool luzP6, bool luzP7,
        string nombreSeleccion)
    {
        if (tecla == null) return;

        if (tecla.wasPressedThisFrame)
        {
            teclaPresionadaActiva = true;
            EnviarDatoSerial("SELECT:" + idObjeto);
            ControlarLucesPedestales(false, false, luzP4, luzP5, luzP6, luzP7, forceSend: true);
            ActivarVibracionControl(0.7f, 1.0f, 0.3f);

            txtEstado.text = "ESTADO: PROP DETECTADO [" + idObjeto + "]";
            txtIndicacion.text = "Mantén presionado para confirmar la selección...";
        }

        if (tecla.wasReleasedThisFrame && teclaPresionadaActiva)
        {
            teclaPresionadaActiva = false;
            OnDecisionMade(nombreSeleccion);
        }
    }

    void StartNextCutscene(string nombreCinematica)
    {
        canReceiveInput = false;
        currentState = GameState.PlayingCutscene;
        StartCoroutine(RoutineCutsceneTimer(nombreCinematica));
    }

    IEnumerator RoutineCutsceneTimer(string nombreCinematica)
    {
        float tiempoRestante = duracionCinematica;

        txtEstado.text = "ESTADO: REPRODUCIENDO CINEMÁTICA";
        txtIndicacion.text = "Proyectando: " + nombreCinematica;

        while (tiempoRestante > 0)
        {
            txtCronometro.text = "Tiempo Restante: " + Mathf.CeilToInt(tiempoRestante).ToString() + "s";
            yield return new WaitForSeconds(1f);
            tiempoRestante -= 1f;
        }

        txtCronometro.text = "Tiempo Restante: 0s";
        yield return new WaitForSeconds(0.5f);

        currentStep++;

        if (currentStep == 1)
        {
            ControlarLucesPedestales(true, true, false, false, false, false, forceSend: true);
            SetWaitingNFCState("Escoge Personaje (Pasa tarjeta):\n[Teo]  |  [Nina]");
        }
        else if (currentStep == 2)
        {
            SetWaitingPageTurnState();
        }
        else if (currentStep == 3)
        {
            StartNextCutscene("Clímax de la historia");
        }
        else
        {
            txtEstado.text = "ESTADO: FINALIZADO";
            txtCronometro.text = "";
            txtIndicacion.text = "¡Experiencia completada!";
            yield return new WaitForSeconds(4f);
            currentStep = 0;
            currentBranch = StoryBranch.Ninguna;
            SetStandbyState();
        }
    }

    void SetStandbyState()
    {
        currentState = GameState.StandbyInicial;
        canReceiveInput = true;
        teclaPresionadaActiva = false;
        CambiarColorFondo(colorStandby);

        ControlarLucesPedestales(false, false, false, false, false, false, forceSend: true);

        txtEstado.text = "ESTADO ACTUAL: STANDBY";
        txtCronometro.text = "Sistema Listo";
        txtIndicacion.text = "PRESIONA [ESPACIO] PARA INICIAR";
    }

    void SetWaitingNFCState(string instrucciones)
    {
        currentState = GameState.WaitingNFC;
        canReceiveInput = true;
        teclaPresionadaActiva = false;

        txtEstado.text = "ESTADO: ESPERANDO TARJETA NFC";
        txtCronometro.text = "En espera de objeto...";
        txtIndicacion.text = instrucciones;
    }

    void SetWaitingPageTurnState()
    {
        currentState = GameState.WaitingPageTurn;
        canReceiveInput = true;
        teclaPresionadaActiva = false;

        txtEstado.text = "ESTADO: PAUSA - CAMBIO DE PÁGINA";
        txtCronometro.text = "Acción física requerida";

        if (currentBranch == StoryBranch.Teo_Fuego)
            txtIndicacion.text = "Gira la página a la Fundidora.\nPresiona [ESPACIO] al terminar.";
        else
            txtIndicacion.text = "Gira la página al Laberinto.\nPresiona [ESPACIO] al terminar.";
    }

    void AvanceDespuesDePagina()
    {
        if (currentBranch == StoryBranch.Teo_Fuego)
        {
            ControlarLucesPedestales(false, false, true, true, false, false, forceSend: true);
            SetWaitingNFCState("Escenario Fundidora - Elegir Prop:\n[ABANICO]  |  [LENTES]");
        }
        else
        {
            ControlarLucesPedestales(false, false, false, false, true, true, forceSend: true);
            SetWaitingNFCState("Escenario Laberinto - Elegir Prop:\n[FLAUTA]  |  [VARITA]");
        }
    }

    void OnDecisionMade(string seleccion)
    {
        StartNextCutscene("Resultado de " + seleccion);
    }

    void CambiarColorFondo(Color nuevoColor)
    {
        if (fondoPantallaUI != null) fondoPantallaUI.color = nuevoColor;
        if (mainCamera != null) mainCamera.backgroundColor = nuevoColor;
    }
}