using UnityEngine;

[CreateAssetMenu(fileName = "ServerConfig", menuName = "Config/ServerConfig")]
public class ServerConfig : ScriptableObject
{
    [Header("SF3D Server Settings")]
    [Tooltip("Local IP address or host name of the PC running the SF3D FastAPI server")]
    public string serverIP = "127.0.0.1";

    [Header("Ports")]
    [Tooltip("Port for sending images to the server")]
    public int uploadPort = 8080;

    [Tooltip("Port for receiving .glb models from the server")]
    public int downloadPort = 8081;

    [Tooltip("If enabled, use a separate port for model downloads (recommended for this backend).")]
    public bool useSeparateDownloadPort = true;

    public string UploadURL => $"http://{serverIP}:{uploadPort}";
    public string DownloadURL => useSeparateDownloadPort
        ? $"http://{serverIP}:{downloadPort}"
        : UploadURL;
}
