using UnityEngine;
using JKTYPE;

/// <summary>
/// Sincroniza el modelo visual del robot URDF con los datos reales del robot JAKA
/// </summary>
public class RobotVisualizer : MonoBehaviour
{
    // ============================================
    // CONFIGURACIÓN
    // ============================================
    [Header("Referencias a las Articulaciones del URDF")]
    [Tooltip("Arrastra aquí los GameObjects de cada Link/Joint del modelo URDF")]
    public Transform[] joints = new Transform[6];
    
    [Header("Configuración")]
    [Tooltip("Suavizado de movimiento (0 = sin suavizado, mayor = más suave)")]
    [Range(0f, 0.5f)]
    public float smoothing = 0.1f;
    
    [Tooltip("Mostrar información de debug en consola")]
    public bool showDebugInfo = false;
    
    [Header("Estado Actual")]
    [SerializeField] private float[] currentAngles = new float[6];
    [SerializeField] private float[] targetAngles = new float[6];
    
    // ============================================
    // VARIABLES PRIVADAS
    // ============================================
    private bool isInitialized = false;
    
    // ============================================
    // UNITY LIFECYCLE
    // ============================================
    
    void Start()
    {
        // Verificar que JakaController existe
        if (JakaController.Instance == null)
        {
            Debug.LogError("[RobotVisualizer] No se encontró JakaController en la escena!");
            enabled = false;
            return;
        }
        
        // Verificar que tenemos referencias a las articulaciones
        if (!ValidateJoints())
        {
            Debug.LogError("[RobotVisualizer] No se configuraron todas las articulaciones!");
            enabled = false;
            return;
        }
        
        // Suscribirse a eventos del controlador
        JakaController.Instance.OnConnected += OnRobotConnected;
        JakaController.Instance.OnDisconnected += OnRobotDisconnected;
        
        isInitialized = true;
        Debug.Log("[RobotVisualizer] Inicializado correctamente.");
    }
    
    void Update()
    {
        if (!isInitialized || !JakaController.Instance.isConnected)
            return;
        
        // Obtener posiciones actuales del robot
        float[] robotAngles = JakaController.Instance.GetCurrentJointPositionsDegrees();
        
        // Actualizar ángulos objetivo
        for (int i = 0; i < 6; i++)
        {
            targetAngles[i] = robotAngles[i];
        }
        
        // Aplicar suavizado y actualizar las articulaciones
        UpdateJointRotations();
        
        // Debug opcional
        if (showDebugInfo && Time.frameCount % 60 == 0) // Cada 60 frames
        {
            LogJointAngles();
        }
    }
    
    void OnDestroy()
    {
        // Desuscribirse de eventos
        if (JakaController.Instance != null)
        {
            JakaController.Instance.OnConnected -= OnRobotConnected;
            JakaController.Instance.OnDisconnected -= OnRobotDisconnected;
        }
    }
    
    // ============================================
    // VALIDACIÓN
    // ============================================
    
    /// <summary>
    /// Verifica que todas las referencias de articulaciones estén asignadas
    /// </summary>
    private bool ValidateJoints()
    {
        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] == null)
            {
                Debug.LogError($"[RobotVisualizer] Joint {i} no está asignado!");
                return false;
            }
        }
        return true;
    }
    
    // ============================================
    // ACTUALIZACIÓN DE VISUALIZACIÓN
    // ============================================
    
    /// <summary>
    /// Actualiza las rotaciones de las articulaciones con suavizado
    /// </summary>
    private void UpdateJointRotations()
    {
        for (int i = 0; i < 6; i++)
        {
            // Interpolar hacia el ángulo objetivo
            if (smoothing > 0)
            {
                currentAngles[i] = Mathf.Lerp(currentAngles[i], targetAngles[i], 1f - smoothing);
            }
            else
            {
                currentAngles[i] = targetAngles[i];
            }
            
            // Aplicar rotación al joint correspondiente
            ApplyRotationToJoint(i, currentAngles[i]);
        }
    }
    
    /// <summary>
    /// Aplica la rotación a una articulación específica
    /// Nota: Ajusta el eje de rotación según la configuración de tu modelo URDF
    /// </summary>
    private void ApplyRotationToJoint(int jointIndex, float angleDegrees)
    {
        if (joints[jointIndex] == null)
            return;
        
        // Los robots JAKA típicamente rotan en el eje Z local
        // Si tu modelo URDF tiene diferentes ejes, ajusta aquí
        Vector3 rotationAxis = GetRotationAxisForJoint(jointIndex);
        joints[jointIndex].localRotation = Quaternion.AngleAxis(angleDegrees, rotationAxis);
    }
    
    /// <summary>
    /// Define el eje de rotación para cada articulación
    /// IMPORTANTE: Esto depende de cómo esté configurado tu modelo URDF
    /// </summary>
    private Vector3 GetRotationAxisForJoint(int jointIndex)
    {
        // Configuración típica de robots JAKA:
        // Joint 0 (base): Rota en Z
        // Joint 1 (hombro): Rota en Y
        // Joint 2 (codo): Rota en Y
        // Joint 3 (muñeca1): Rota en Z
        // Joint 4 (muñeca2): Rota en Y
        // Joint 5 (muñeca3): Rota en Z
        
        switch (jointIndex)
        {
            case 0: return Vector3.forward;  // Z
            case 1: return Vector3.up;       // Y
            case 2: return Vector3.up;       // Y
            case 3: return Vector3.forward;  // Z
            case 4: return Vector3.up;       // Y
            case 5: return Vector3.forward;  // Z
            default: return Vector3.forward;
        }
    }
    
    // ============================================
    // EVENTOS
    // ============================================
    
    private void OnRobotConnected()
    {
        Debug.Log("[RobotVisualizer] Robot conectado. Iniciando sincronización visual.");
    }
    
    private void OnRobotDisconnected()
    {
        Debug.Log("[RobotVisualizer] Robot desconectado. Deteniendo sincronización visual.");
    }
    
    // ============================================
    // DEBUG
    // ============================================
    
    /// <summary>
    /// Muestra los ángulos actuales de las articulaciones
    /// </summary>
    private void LogJointAngles()
    {
        string info = "[RobotVisualizer] Ángulos actuales:\n";
        for (int i = 0; i < 6; i++)
        {
            info += $"  Joint {i}: {currentAngles[i]:F2}°\n";
        }
        Debug.Log(info);
    }
    
    // ============================================
    // MÉTODOS PÚBLICOS AUXILIARES
    // ============================================
    
    /// <summary>
    /// Asigna automáticamente las articulaciones buscándolas por nombre
    /// Útil si tu URDF tiene nombres estándar como "Link1", "Link2", etc.
    /// </summary>
    [ContextMenu("Auto-Asignar Articulaciones por Nombre")]
    public void AutoAssignJointsByName()
    {
        // Buscar en el modelo URDF
        Transform urdfRoot = transform;
        
        // Nombres típicos en modelos URDF de JAKA
        string[] jointNames = { "Link1", "Link2", "Link3", "Link4", "Link5", "Link6" };
        
        for (int i = 0; i < jointNames.Length; i++)
        {
            Transform found = FindDeepChild(urdfRoot, jointNames[i]);
            if (found != null)
            {
                joints[i] = found;
                Debug.Log($"Asignado Joint {i}: {found.name}");
            }
            else
            {
                Debug.LogWarning($"No se encontró articulación con nombre: {jointNames[i]}");
            }
        }
    }
    
    /// <summary>
    /// Busca recursivamente un hijo por nombre
    /// </summary>
    private Transform FindDeepChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Contains(childName))
                return child;
            
            Transform result = FindDeepChild(child, childName);
            if (result != null)
                return result;
        }
        return null;
    }
    
    /// <summary>
    /// Resetea el modelo a la posición inicial (todos los joints en 0)
    /// </summary>
    [ContextMenu("Resetear Posición")]
    public void ResetPosition()
    {
        for (int i = 0; i < 6; i++)
        {
            currentAngles[i] = 0;
            targetAngles[i] = 0;
            if (joints[i] != null)
            {
                joints[i].localRotation = Quaternion.identity;
            }
        }
        Debug.Log("[RobotVisualizer] Posición reseteada.");
    }
}