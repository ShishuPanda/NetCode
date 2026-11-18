using System.Collections;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

/// <summary>
/// Data model class representing the API payload.
/// </summary>
[System.Serializable]
public class ApiData
{
    public int userId;
    public int id;
    public string title;
    public bool completed;
}

/// <summary>
/// Minimal API manager demonstrating GET (fetch) and POST (push)
/// using both async/await and Coroutine, toggled via a bool checkbox.
/// </summary>
public class SimpleApiManager : MonoBehaviour
{
    [Header("Mode Selection")]
    [Tooltip("Check to run via async/await, uncheck to run via Coroutine")]
    public bool useAsync = true;

    [Header("Endpoints")]
    public string getUrl = "https://jsonplaceholder.typicode.com/todos/1";
    public string postUrl = "https://jsonplaceholder.typicode.com/posts";

    [Header("Data (Assigned from API)")]
    public ApiData currentData;

    private string statusLog = "Ready. Press 'Execute API' to test.";

    private void Start()
    {
        Execute();
    }

    [ContextMenu("Execute API")]
    public void Execute()
    {
        if (useAsync)
        {
            _ = RunAsync();
        }
        else
        {
            StartCoroutine(RunCoroutine());
        }
    }

    // -------------------------------------------------------------
    // 1. ASYNC / AWAIT IMPLEMENTATION
    // -------------------------------------------------------------
    public async Task RunAsync()
    {
        statusLog = "[Async/Await] 1. Fetching GET from API...";
        Debug.Log(statusLog);

        // A. Fetch GET
        using (UnityWebRequest getReq = UnityWebRequest.Get(getUrl))
        {
            var op = getReq.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (getReq.result != UnityWebRequest.Result.Success)
            {
                statusLog = $"[Async/Await] GET Error: {getReq.error}";
                Debug.LogError(statusLog);
                return;
            }

            // Assign JSON response to Class
            currentData = JsonUtility.FromJson<ApiData>(getReq.downloadHandler.text);
            statusLog = $"[Async/Await] GET Success!\nParsed into class:\n- ID: {currentData.id}\n- Title: {currentData.title}\n- Completed: {currentData.completed}";
            Debug.Log(statusLog);
        }

        // Modify class data before pushing back
        currentData.title = "Updated Title from Unity (Async)";

        // B. Push POST from Class
        statusLog += "\n\n[Async/Await] 2. Pushing POST to API...";
        Debug.Log(statusLog);

        string jsonPayload = JsonUtility.ToJson(currentData);
        using (UnityWebRequest postReq = new UnityWebRequest(postUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            postReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            postReq.downloadHandler = new DownloadHandlerBuffer();
            postReq.SetRequestHeader("Content-Type", "application/json");

            var op = postReq.SendWebRequest();
            while (!op.isDone) await Task.Yield();

            if (postReq.result != UnityWebRequest.Result.Success)
            {
                statusLog = $"[Async/Await] POST Error: {postReq.error}";
                Debug.LogError(statusLog);
            }
            else
            {
                statusLog += $"\n[Async/Await] POST Success! Status Code: {postReq.responseCode}\nResponse: {postReq.downloadHandler.text}";
                Debug.Log(statusLog);
            }
        }
    }

    // -------------------------------------------------------------
    // 2. COROUTINE IMPLEMENTATION
    // -------------------------------------------------------------
    public IEnumerator RunCoroutine()
    {
        statusLog = "[Coroutine] 1. Fetching GET from API...";
        Debug.Log(statusLog);

        // A. Fetch GET
        using (UnityWebRequest getReq = UnityWebRequest.Get(getUrl))
        {
            yield return getReq.SendWebRequest();

            if (getReq.result != UnityWebRequest.Result.Success)
            {
                statusLog = $"[Coroutine] GET Error: {getReq.error}";
                Debug.LogError(statusLog);
                yield break;
            }

            // Assign JSON response to Class
            currentData = JsonUtility.FromJson<ApiData>(getReq.downloadHandler.text);
            statusLog = $"[Coroutine] GET Success!\nParsed into class:\n- ID: {currentData.id}\n- Title: {currentData.title}\n- Completed: {currentData.completed}";
            Debug.Log(statusLog);
        }

        // Modify class data before pushing back
        currentData.title = "Updated Title from Unity (Coroutine)";

        // B. Push POST from Class
        statusLog += "\n\n[Coroutine] 2. Pushing POST to API...";
        Debug.Log(statusLog);

        string jsonPayload = JsonUtility.ToJson(currentData);
        using (UnityWebRequest postReq = new UnityWebRequest(postUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            postReq.uploadHandler = new UploadHandlerRaw(bodyRaw);
            postReq.downloadHandler = new DownloadHandlerBuffer();
            postReq.SetRequestHeader("Content-Type", "application/json");

            yield return postReq.SendWebRequest();

            if (postReq.result != UnityWebRequest.Result.Success)
            {
                statusLog = $"[Coroutine] POST Error: {postReq.error}";
                Debug.LogError(statusLog);
            }
            else
            {
                statusLog += $"\n[Coroutine] POST Success! Status Code: {postReq.responseCode}\nResponse: {postReq.downloadHandler.text}";
                Debug.Log(statusLog);
            }
        }
    }

    // Simple on-screen visual interface
    private void OnGUI()
    {
        float width = 520;
        float height = 375;
        GUILayout.BeginArea(new Rect(20, 20, width, height), GUI.skin.window);
        GUILayout.Label("<b><size=15>SUPER SIMPLE API DEMO</size></b>");
        GUILayout.Label("<color=grey>Fetch API ➔ Assign to Class ➔ Push from Class</color>");
        GUILayout.Space(8);

        useAsync = GUILayout.Toggle(useAsync, " <b>Use Async / Await</b> (Uncheck to use Coroutine)");
        GUILayout.Space(6);

        string buttonText = useAsync ? "Execute API via [Async / Await]" : "Execute API via [Coroutine]";
        if (GUILayout.Button(buttonText, GUILayout.Height(36)))
        {
            Execute();
        }

        GUILayout.Space(10);
        GUILayout.Label("<b>Execution Output & Network Response:</b>");
        GUILayout.TextArea(statusLog, GUILayout.Height(150));

        GUILayout.Space(6);
        if (GUILayout.Button("Back to Lobby Scene", GUILayout.Height(26)))
        {
            SceneManager.LoadScene("LobbyScene");
        }

        GUILayout.EndArea();
    }
}
