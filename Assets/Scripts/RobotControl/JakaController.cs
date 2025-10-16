using UnityEngine;
using JKTYPE;
using System;
using System.Threading;

/// <summary>
/// Controlador principal del robot JAKA
/// Gestiona la conexión, lectura de datos y sincronización
/// Patrón Singleton para acceso global
/// </summary>
public class JakaController : MonoBehaviour
{
    // ============================================
    // SINGLETON
    // ============================================
    public static JakaController Instance { get; private set; }

    // ============================================
    // CONFIGURACIÓN
    // ============================================
    [Header("Configuración de Conexión")]
    [Tooltip("IP del robot JAKA (simulador o físico)")]
    public string robotIP = "192.168.171.128";
    
    [Tooltip("Frecuencia de actualización de datos (Hz)")]
    [Range(10, 125)]
    public int updateFrequency = 60;

    [Header("Estado")]
    public bool isConnected = false;
    public bool isRobotEnabled = false;
    public bool isRobotMoving = false;
    
    // ============================================
    // VARIABLES PRIVADAS
    // ============================================
    private int robotHandle = 0;
    private Thread dataReadThread;
    private bool shouldReadData = false;
    
    // Datos actuales del robot (thread-safe)
    private readonly object dataLock = new object();
    private JointValue currentJointPositions;
    private CartesianPose currentTCPPosition;
    private RobotStatus currentStatus;
    
    // ============================================
    // EVENTOS
    // ============================================
    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<JointValue> OnJointPositionsUpdated;
    public event Action<CartesianPose> OnTCPPositionUpdated;

    // ============================================
    // UNITY LIFECYCLE
    // ============================================
    
    void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // Inicializar estructuras
        currentJointPositions = new JointValue(0, 0, 0, 0, 0, 0);
    }

    void Start()
    {
        Log("JakaController inicializado. Listo para conectar.");
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }

    // ============================================
    // CONEXIÓN
    // ============================================
    
    /// <summary>
    /// Conecta con el robot JAKA
    /// </summary>
    public bool Connect()
    {
        if (isConnected)
        {
            LogWarning("Ya existe una conexión activa.");
            return true;
        }

        Log($"Intentando conectar con el robot en {robotIP}...");
        
        int result = JakaAPI.create_handler(robotIP, ref robotHandle);
        
        if (JakaAPI.IsError(result))
        {
            LogError($"Error al crear handler: {JakaAPI.GetErrorDescription(result)}");
            return false;
        }

        isConnected = true;
        Log($"✓ Conexión establecida. Handle: {robotHandle}");
        
        // Verificar estado del robot
        UpdateRobotStatus();
        
        // Iniciar lectura de datos en background
        StartDataReading();
        
        OnConnected?.Invoke();
        return true;
    }

    /// <summary>
    /// Desconecta del robot
    /// </summary>
    public void Disconnect()
    {
        if (!isConnected) return;

        Log("Desconectando del robot...");
        
        // Detener lectura de datos
        StopDataReading();
        
        // Deshabilitar robot si está habilitado
        if (isRobotEnabled)
        {
            DisableRobot();
        }
        
        // Destruir handler
        JakaAPI.destory_handler(ref robotHandle);
        
        isConnected = false;
        robotHandle = 0;
        
        Log("✓ Desconexión exitosa.");
        OnDisconnected?.Invoke();
    }

    // ============================================
    // CONTROL DE ENERGÍA
    // ============================================
    
    /// <summary>
    /// Enciende el robot
    /// </summary>
    public bool PowerOn()
    {
        if (!isConnected)
        {
            LogError("No hay conexión activa.");
            return false;
        }

        Log("Encendiendo robot...");
        int result = JakaAPI.power_on(ref robotHandle);
        
        if (JakaAPI.IsError(result))
        {
            LogError($"Error al encender: {JakaAPI.GetErrorDescription(result)}");
            return false;
        }

        Log("✓ Robot encendido.");
        UpdateRobotStatus();
        return true;
    }

    /// <summary>
    /// Habilita el robot para movimiento
    /// </summary>
    public bool EnableRobot()
    {
        if (!isConnected)
        {
            LogError("No hay conexión activa.");
            return false;
        }

        Log("Habilitando robot...");
        int result = JakaAPI.enable_robot(ref robotHandle);
        
        if (JakaAPI.IsError(result))
        {
            LogError($"Error al habilitar: {JakaAPI.GetErrorDescription(result)}");
            return false;
        }

        isRobotEnabled = true;
        Log("✓ Robot habilitado y listo para moverse.");
        UpdateRobotStatus();
        return true;
    }

    /// <summary>
    /// Deshabilita el robot
    /// </summary>
    public bool DisableRobot()
    {
        if (!isConnected) return false;

        Log("Deshabilitando robot...");
        int result = JakaAPI.disable_robot(ref robotHandle);
        
        if (JakaAPI.IsError(result))
        {
            LogError($"Error al deshabilitar: {JakaAPI.GetErrorDescription(result)}");
            return false;
        }

        isRobotEnabled = false;
        Log("✓ Robot deshabilitado.");
        return true;
    }

    // ============================================
    // LECTURA DE DATOS (THREAD SEGURO)
    // ============================================
    
    /// <summary>
    /// Inicia el hilo de lectura continua de datos
    /// </summary>
    private void StartDataReading()
    {
        if (dataReadThread != null && dataReadThread.IsAlive)
            return;

        shouldReadData = true;
        dataReadThread = new Thread(DataReadLoop);
        dataReadThread.IsBackground = true;
        dataReadThread.Start();
        
        Log($"Iniciando lectura de datos a {updateFrequency} Hz");
    }

    /// <summary>
    /// Detiene el hilo de lectura de datos
    /// </summary>
    private void StopDataReading()
    {
        shouldReadData = false;
        
        if (dataReadThread != null && dataReadThread.IsAlive)
        {
            dataReadThread.Join(1000); // Esperar máximo 1 segundo
        }
        
        dataReadThread = null;
    }

    /// <summary>
    /// Bucle principal de lectura de datos (corre en thread separado)
    /// </summary>
    private void DataReadLoop()
    {
        int sleepTime = 1000 / updateFrequency;
        
        while (shouldReadData && isConnected)
        {
            try
            {
                // Leer posiciones de articulaciones
                JointValue joints = new JointValue();
                joints.jVal = new double[6];
                int result = JakaAPI.get_joint_position(ref robotHandle, ref joints);
                
                if (!JakaAPI.IsError(result))
                {
                    lock (dataLock)
                    {
                        currentJointPositions = joints;
                    }
                }

                // Leer posición TCP
                CartesianPose tcp = new CartesianPose();
                result = JakaAPI.get_tcp_position(ref robotHandle, ref tcp);
                
                if (!JakaAPI.IsError(result))
                {
                    lock (dataLock)
                    {
                        currentTCPPosition = tcp;
                    }
                }

                Thread.Sleep(sleepTime);
            }
            catch (ThreadAbortException)
            {
                break;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error en DataReadLoop: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Actualiza el estado del robot
    /// </summary>
    private void UpdateRobotStatus()
    {
        if (!isConnected) return;

        RobotStatus status = new RobotStatus();
        int result = JakaAPI.get_robot_status(ref robotHandle, ref status);
        
        if (!JakaAPI.IsError(result))
        {
            lock (dataLock)
            {
                currentStatus = status;
                isRobotMoving = status.is_moving == 1;
            }
        }
    }

    // ============================================
    // GETTERS PÚBLICOS (Thread-safe)
    // ============================================
    
    /// <summary>
    /// Obtiene las posiciones actuales de las articulaciones en radianes
    /// </summary>
    public JointValue GetCurrentJointPositions()
    {
        lock (dataLock)
        {
            return currentJointPositions;
        }
    }

    /// <summary>
    /// Obtiene las posiciones actuales de las articulaciones en grados
    /// </summary>
    public float[] GetCurrentJointPositionsDegrees()
    {
        lock (dataLock)
        {
            float[] degrees = new float[6];
            for (int i = 0; i < 6; i++)
            {
                degrees[i] = (float)(currentJointPositions.jVal[i] * Mathf.Rad2Deg);
            }
            return degrees;
        }
    }

    /// <summary>
    /// Obtiene la posición cartesiana actual del TCP
    /// </summary>
    public CartesianPose GetCurrentTCPPosition()
    {
        lock (dataLock)
        {
            return currentTCPPosition;
        }
    }

    /// <summary>
    /// Obtiene el estado actual del robot
    /// </summary>
    public RobotStatus GetRobotStatus()
    {
        lock (dataLock)
        {
            return currentStatus;
        }
    }

    // ============================================
    // COMANDOS DE MOVIMIENTO
    // ============================================
    
    /// <summary>
    /// Mueve una articulación específica en modo JOG
    /// </summary>
    public bool JogJoint(int jointIndex, double velocity, double position)
    {
        if (!isConnected || !isRobotEnabled)
        {
            LogWarning("Robot no está listo para moverse.");
            return false;
        }

        if (jointIndex < 0 || jointIndex > 5)
        {
            LogError($"Índice de articulación inválido: {jointIndex}");
            return false;
        }

        int result = JakaAPI.jog(
            ref robotHandle, 
            jointIndex, 
            MoveMode.INCR, 
            CoordType.COORD_JOINT, 
            velocity, 
            position
        );

        return !JakaAPI.IsError(result);
    }

    /// <summary>
    /// Detiene el movimiento JOG de una articulación
    /// </summary>
    public bool StopJog(int jointIndex)
    {
        if (!isConnected) return false;

        int result = JakaAPI.jog_stop(ref robotHandle, jointIndex);
        return !JakaAPI.IsError(result);
    }

    // ============================================
    // LOGGING
    // ============================================
    
    private void Log(string message)
    {
        Debug.Log($"[JakaController] {message}");
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[JakaController] {message}");
    }

    private void LogError(string message)
    {
        Debug.LogError($"[JakaController] {message}");
    }
}