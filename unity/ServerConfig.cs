using UnityEngine;

/// <summary>
/// Central config for the SF3D server connection.
/// Attach once to any persistent GameObject in the scene.
///
/// Upload Port  8080 → Unity sends image    → POST /generate
/// Download Port 8081 → Unity receives .glb → GET  /download/{job_id}
/// </summary>
public class ServerConfig : MonoBehaviour
{
    [Header("Server Connection")]
    [Tooltip("IP of the PC running backend/server.py — use 127.0.0.1 if same machine")]
    public string serverIP = "127.0.0.1";

    [Tooltip("Port for sending the image to the server (POST /generate)")]
    public int uploadPort = 8080;

    [Tooltip("Port for downloading the generated .glb model (GET /download/{job_id})")]
    public int downloadPort = 8081;

    // ── Convenience properties ────────────────────────────────────────────────
    public string UploadUrl => $"http://{serverIP}:{uploadPort}";
    public string DownloadUrl => $"http://{serverIP}:{downloadPort}";
}
