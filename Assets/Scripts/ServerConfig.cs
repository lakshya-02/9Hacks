using UnityEngine;

[CreateAssetMenu(fileName = "ServerConfig", menuName = "Config/ServerConfig")]
public class ServerConfig : ScriptableObject
{
    [Header("SF3D Server Settings")]
    [Tooltip("Local IP address of the PC running the SF3D Flask server")]
    public string serverIP = "192.168.1.100";

    [Header("Ports")]
    [Tooltip("Port for sending images to the server")]
    public int uploadPort = 8080;

    [Tooltip("Port for receiving .glb models from the server")]
    public int downloadPort = 8081;

    public string UploadURL => $"http://{serverIP}:{uploadPort}";
    public string DownloadURL => $"http://{serverIP}:{downloadPort}";
}
