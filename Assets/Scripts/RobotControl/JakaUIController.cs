using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JKTYPE;

/// <summary>
/// Controlador de la interfaz de usuario para el robot JAKA
/// Gestiona botones, logs y visualización de estado
/// </summary>
public class JakaUIController : MonoBehaviour
{
    // ============================================
    // REFERENCIAS UI
    // ============================================
    [Header("Botones de Control")]
    public Button connectButton;
    public Button disconnectButton;
    public Button powerOnButton;
    public Button enableButton;
    public Button disableButton;
    
    [Header("Textos de Estado")]
    public TextMeshProUGUI statusText;
    public TextMeshProUGUI connectionStatusText;
    public TextMeshProUGUI jointAnglesText;
    
    [Header("Input de IP")]
    public TMP_InputField ipInputField;
    
    [Header("Panel de Logs")]
    public TextMeshProUGUI logText;
    public ScrollRect logScrollRect;
    public int maxLogLines = 20;
    
    [Header("Configuración")]
    public Color connectedColor = Color.green;
    public Color disconnectedColor = Color.red;
    public Color warningColor = Color.yellow;
    
    // ============================================
    // VARIABLES PRIVADAS
    // ============================================
    private string logBuffer = "";
    private int logLineCount = 0;
    
    // ============================================
    // UNITY LIFECYCLE
    // ============================================
    
    void Start()
    {
        // Verificar JakaController
        if (JakaController.Instance == null)
        {
            LogError("JakaController no encontrado en la escena!");
            return;
        }
        
        // Configurar listeners de botones
        SetupButtonListeners();
        
        // Suscribirse a eventos
        JakaController.Instance.OnConnected += OnRobotConnected;
        JakaController.Instance.OnDisconnected += OnRobotDisconnected;
        
        // Estado inicial
        UpdateUIState();
        
        // Cargar IP guardada o usar default
        if (ipInputField != null)
        {
            ipInputField.text = JakaController.Instance.robotIP;
        }
        
        LogInfo("Sistema inicializado. Presiona 'Conectar' para comenzar.");
    }
    
    void Update()
    {
        // Actualizar información en tiempo real
        UpdateStatusDisplay();
        UpdateJointAnglesDisplay();
    }
    
    void OnDestroy()
    {
        // Limpiar suscripciones
        if (JakaController.Instance != null)
        {
            JakaController.Instance.OnConnected -= OnRobotConnected;
            JakaController.Instance.OnDisconnected -= OnRobotDisconnected;
        }
    }
    
    // ============================================
    // CONFIGURACIÓN DE BOTONES
    // ============================================
    
    private void SetupButtonListeners()
    {
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectButtonClicked);
        
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(OnDisconnectButtonClicked);
        
        if (powerOnButton != null)
            powerOnButton.onClick.AddListener(OnPowerOnButtonClicked);
        
        if (enableButton != null)
            enableButton.onClick.AddListener(OnEnableButtonClicked);
        
        if (disableButton != null)
            disableButton.onClick.AddListener(OnDisableButtonClicked);
    }
    
    // ============================================
    // CALLBACKS DE BOTONES
    // ============================================
    
    private void OnConnectButtonClicked()
    {
        // Actualizar IP si cambió
        if (ipInputField != null && !string.IsNullOrEmpty(ipInputField.text))
        {
            JakaController.Instance.robotIP = ipInputField.text;
        }
        
        LogInfo($"Intentando conectar a {JakaController.Instance.robotIP}...");
        
        bool success = JakaController.Instance.Connect();
        if (!success)
        {
            LogError("Falló la conexión. Verifica que el simulador esté corriendo.");
        }
    }
    
    private void OnDisconnectButtonClicked()
    {
        LogInfo("Desconectando...");
        JakaController.Instance.Disconnect();
    }
    
    private void OnPowerOnButtonClicked()
    {
        LogInfo("Encendiendo robot...");
        bool success = JakaController.Instance.PowerOn();
        if (success)
        {
            LogSuccess("Robot encendido correctamente.");
        }
    }
    
    private void OnEnableButtonClicked()
    {
        LogInfo("Habilitando robot...");
        bool success = JakaController.Instance.EnableRobot();
        if (success)
        {
            LogSuccess("Robot habilitado y listo para moverse.");
        }
    }
    
    private void OnDisableButtonClicked()
    {
        LogInfo("Deshabilitando robot...");
        bool success = JakaController.Instance.DisableRobot();
        if (success)
        {
            LogSuccess("Robot deshabilitado.");
        }
    }
    
    // ============================================
    // EVENTOS DEL ROBOT
    // ============================================
    
    private void OnRobotConnected()
    {
        LogSuccess("✓ Conexión establecida exitosamente!");
        UpdateUIState();
    }
    
    private void OnRobotDisconnected()
    {
        LogInfo("Desconectado del robot.");
        UpdateUIState();
    }
    
    // ============================================
    // ACTUALIZACIÓN DE UI
    // ============================================
    
    private void UpdateUIState()
    {
        bool isConnected = JakaController.Instance != null && JakaController.Instance.isConnected;
        bool isEnabled = JakaController.Instance != null && JakaController.Instance.isRobotEnabled;
        
        // Habilitar/deshabilitar botones según estado
        if (connectButton != null)
            connectButton.interactable = !isConnected;
        
        if (disconnectButton != null)
            disconnectButton.interactable = isConnected;
        
        if (powerOnButton != null)
            powerOnButton.interactable = isConnected;
        
        if (enableButton != null)
            enableButton.interactable = isConnected && !isEnabled;
        
        if (disableButton != null)
            disableButton.interactable = isConnected && isEnabled;
        
        // Actualizar texto de estado de conexión
        if (connectionStatusText != null)
        {
            if (isConnected)
            {
                connectionStatusText.text = "● CONECTADO";
                connectionStatusText.color = connectedColor;
            }
            else
            {
                connectionStatusText.text = "● DESCONECTADO";
                connectionStatusText.color = disconnectedColor;
            }
        }
    }
    
    private void UpdateStatusDisplay()
    {
        if (statusText == null || JakaController.Instance == null || !JakaController.Instance.isConnected)
            return;
        
        RobotStatus status = JakaController.Instance.GetRobotStatus();
        
        string statusInfo = "ESTADO DEL ROBOT\n";
        statusInfo += $"Encendido: {(status.powered_on == 1 ? "✓" : "✗")}\n";
        statusInfo += $"Habilitado: {(status.enabled == 1 ? "✓" : "✗")}\n";
        statusInfo += $"En movimiento: {(status.is_moving == 1 ? "Sí" : "No")}\n";
        statusInfo += $"Error: {(status.in_error == 1 ? "SÍ" : "No")}\n";
        
        if (status.error_code != 0)
        {
            statusInfo += $"Código de error: {status.error_code}";
            statusText.color = warningColor;
        }
        else
        {
            statusText.color = Color.white;
        }
        
        statusText.text = statusInfo;
    }
    
    private void UpdateJointAnglesDisplay()
    {
        if (jointAnglesText == null || JakaController.Instance == null || !JakaController.Instance.isConnected)
            return;
        
        float[] angles = JakaController.Instance.GetCurrentJointPositionsDegrees();
        
        string anglesInfo = "POSICIONES DE ARTICULACIONES\n";
        for (int i = 0; i < angles.Length; i++)
        {
            anglesInfo += $"Joint {i + 1}: {angles[i]:F2}°\n";
        }
        
        jointAnglesText.text = anglesInfo;
    }
    
    // ============================================
    // SISTEMA DE LOGS
    // ============================================
    
    public void LogInfo(string message)
    {
        AddLogLine($"[INFO] {message}", Color.white);
    }
    
    public void LogSuccess(string message)
    {
        AddLogLine($"[OK] {message}", connectedColor);
    }
    
    public void LogWarning(string message)
    {
        AddLogLine($"[WARN] {message}", warningColor);
    }
    
    public void LogError(string message)
    {
        AddLogLine($"[ERROR] {message}", disconnectedColor);
    }
    
    private void AddLogLine(string message, Color color)
    {
        if (logText == null) return;
        
        // Añadir timestamp
        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string colorHex = ColorUtility.ToHtmlStringRGB(color);
        string formattedMessage = $"<color=#{colorHex}>[{timestamp}] {message}</color>";
        
        // Añadir al buffer
        logBuffer += formattedMessage + "\n";
        logLineCount++;
        
        // Limitar líneas
        if (logLineCount > maxLogLines)
        {
            int firstNewLine = logBuffer.IndexOf('\n');
            if (firstNewLine > 0)
            {
                logBuffer = logBuffer.Substring(firstNewLine + 1);
                logLineCount--;
            }
        }
        
        // Actualizar texto
        logText.text = logBuffer;
        
        // Auto-scroll al final
        if (logScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            logScrollRect.verticalNormalizedPosition = 0f;
        }
        
        // También log en consola de Unity
        Debug.Log(message);
    }
    
    // ============================================
    // MÉTODOS PÚBLICOS
    // ============================================
    
    public void ClearLogs()
    {
        logBuffer = "";
        logLineCount = 0;
        if (logText != null)
            logText.text = "";
    }
}