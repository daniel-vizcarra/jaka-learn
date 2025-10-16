using System;
using System.Runtime.InteropServices;

namespace JKTYPE
{
    // ============================================
    // ESTRUCTURAS DE DATOS
    // ============================================
    
    [StructLayout(LayoutKind.Sequential)]
    public struct JointValue
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
        public double[] jVal;
        
        public JointValue(double j1, double j2, double j3, double j4, double j5, double j6)
        {
            jVal = new double[] { j1, j2, j3, j4, j5, j6 };
        }
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct CartesianPose
    {
        public double x, y, z, rx, ry, rz;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct RobotState
    {
        public int state;  // 0=apagado, 1=encendido, 2=habilitado, etc.
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct RobotStatus
    {
        public int powered_on;     // 1 si está encendido
        public int enabled;        // 1 si está habilitado
        public int error_code;     // Código de error (0 = sin error)
        public int in_error;       // 1 si hay error
        public int is_moving;      // 1 si está en movimiento
        public int in_collision;   // 1 si detectó colisión
    }
    
    // ============================================
    // ENUMERACIONES
    // ============================================
    
    public enum MoveMode 
    { 
        ABS = 0,   // Absoluto
        INCR = 1   // Incremental
    }
    
    public enum CoordType 
    { 
        COORD_BASE = 0,   // Sistema de coordenadas base
        COORD_JOINT = 1,  // Sistema de coordenadas de articulación
        COORD_TOOL = 2    // Sistema de coordenadas de herramienta
    }
}

/// <summary>
/// Wrapper de la API de JAKA para Unity
/// Versión mejorada con funciones adicionales para lectura de datos
/// </summary>
public static class JakaAPI
{
    private const string DllName = "jakaAPI.dll";

    // ============================================
    // GESTIÓN DE CONEXIÓN
    // ============================================
    
    /// <summary>
    /// Crea un handler de conexión con el robot
    /// </summary>
    /// <param name="ip">IP del robot (ej: "192.168.171.128")</param>
    /// <param name="handle">Handle de salida (referencia)</param>
    /// <param name="use_grpc">Usar gRPC (por defecto false)</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int create_handler(
        [MarshalAs(UnmanagedType.LPStr)] string ip, 
        ref int handle, 
        bool use_grpc = false
    );

    /// <summary>
    /// Destruye el handler y cierra la conexión
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int destory_handler(ref int handle);

    // ============================================
    // CONTROL DE ENERGÍA
    // ============================================
    
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int power_on(ref int handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int power_off(ref int handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int shut_down(ref int handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int enable_robot(ref int handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int disable_robot(ref int handle);

    // ============================================
    // LECTURA DE ESTADO
    // ============================================
    
    /// <summary>
    /// Obtiene el estado básico del robot
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_robot_state(ref int handle, ref JKTYPE.RobotState state);

    /// <summary>
    /// Obtiene el estado detallado del robot
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_robot_status(ref int handle, ref JKTYPE.RobotStatus status);

    // ============================================
    // LECTURA DE POSICIONES
    // ============================================
    
    /// <summary>
    /// Obtiene las posiciones actuales de las articulaciones (en radianes)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_joint_position(ref int handle, ref JKTYPE.JointValue jVal);

    /// <summary>
    /// Obtiene la posición cartesiana actual del TCP (Tool Center Point)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int get_tcp_position(ref int handle, ref JKTYPE.CartesianPose pose);

    // ============================================
    // CONTROL DE MOVIMIENTO
    // ============================================
    
    /// <summary>
    /// Movimiento JOG (manual) de una articulación o eje cartesiano
    /// </summary>
    /// <param name="aj_num">Número de articulación (0-5) o eje (0-5 para X,Y,Z,Rx,Ry,Rz)</param>
    /// <param name="move_mode">Modo absoluto o incremental</param>
    /// <param name="coord_type">Tipo de coordenadas (joint, base, tool)</param>
    /// <param name="vel_cmd">Velocidad del movimiento</param>
    /// <param name="pos_cmd">Posición objetivo (solo para modo ABS)</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jog(
        ref int handle, 
        int aj_num, 
        JKTYPE.MoveMode move_mode, 
        JKTYPE.CoordType coord_type, 
        double vel_cmd, 
        double pos_cmd
    );

    /// <summary>
    /// Detiene el movimiento JOG
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jog_stop(ref int handle, int num);

    /// <summary>
    /// Movimiento a posición de articulaciones especificada
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int joint_move(
        ref int handle, 
        ref JKTYPE.JointValue joint_pos, 
        JKTYPE.MoveMode move_mode, 
        bool is_block
    );

    /// <summary>
    /// Movimiento lineal en espacio cartesiano
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int linear_move(
        ref int handle, 
        ref JKTYPE.CartesianPose target_pos, 
        JKTYPE.MoveMode move_mode, 
        bool is_block
    );

    // ============================================
    // UTILIDADES
    // ============================================
    
    /// <summary>
    /// Verifica si hay un error basado en el código de retorno
    /// </summary>
    public static bool IsError(int returnCode)
    {
        return returnCode != 0;
    }
    
    /// <summary>
    /// Obtiene una descripción básica del código de error
    /// </summary>
    public static string GetErrorDescription(int errorCode)
    {
        switch (errorCode)
        {
            case 0: return "Sin error";
            case -1: return "Error de conexión";
            case -2: return "Parámetro inválido";
            case -3: return "Robot no encendido";
            case -4: return "Robot no habilitado";
            case -5: return "Robot en error";
            default: return $"Error desconocido (código: {errorCode})";
        }
    }
}