using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BallSorter : MonoBehaviour
{
    [Header("Game Settings")]
    public List<Tube> tubes;
    public List<GameObject> ballPrefabs;
    public Button resetButton;
    public GameObject WinUI;
    public int scrambleMoves = 10;

    [Header("Generation Settings")]
    public int tubeCount = 5;
    public int ballsPerTube = 4;
    public int colorCount = 4;

    private Tube selectedTube = null;
    private List<List<Color>> initialSetup;

    void Start()
    {
        resetButton.onClick.AddListener(ResetLevel);
        GeneratePuzzle();
    }

    void Update()
    {
        HandlePlayerInput();
    }

    #region Level Generation
    void GeneratePuzzle()
    {
        // Clear existing tubes
        foreach (Tube tube in tubes)
        {
            tube.ClearBalls();
        }

        // Tạo danh sách tất cả bóng cần sinh (4 bóng mỗi màu)
        List<GameObject> allBalls = new List<GameObject>();
        for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
        {
            GameObject prefab = ballPrefabs[colorIndex % ballPrefabs.Count];
            for (int i = 0; i < ballsPerTube; i++)
            {
                allBalls.Add(prefab);
            }
        }

        // Xáo trộn danh sách bóng
        Shuffle(allBalls);

        // Phân bố bóng vào các ống (4 ống đầu chứa bóng, ống cuối rỗng)
        for (int tubeIndex = 0; tubeIndex < tubes.Count; tubeIndex++)
        {
            if (tubeIndex < colorCount) // Chỉ phân bố vào 4 ống đầu
            {
                for (int ballIndex = 0; ballIndex < ballsPerTube; ballIndex++)
                {
                    int listIndex = tubeIndex * ballsPerTube + ballIndex;
                    if (listIndex < allBalls.Count)
                    {
                        GameObject prefab = allBalls[listIndex];
                        Vector3 pos = tubes[tubeIndex].GetBallPosition(ballIndex);
                        GameObject ball = Instantiate(prefab, pos, Quaternion.identity, tubes[tubeIndex].transform);
                        tubes[tubeIndex].balls.Add(ball);
                    }
                }
            }
        }
        EnsureSolvable();
        StartCoroutine(AutoSolveAndGenerate());
        SaveInitialSetup();
    }

    void EnsureSolvable()
    {
        foreach (Tube tube in tubes)
        {
            if (tube.IsSameColor() && tube.balls.Count == ballsPerTube)
            {
                // Nếu ống đã hoàn thành ngay từ đầu, đổi 1 bóng với ống khác
                foreach (Tube otherTube in tubes)
                {
                    if (otherTube != tube && !otherTube.IsEmpty)
                    {
                        // Hoán đổi bóng trên cùng giữa 2 ống
                        GameObject ball1 = tube.balls.Last();
                        GameObject ball2 = otherTube.balls.Last();

                        tube.balls.Remove(ball1);
                        otherTube.balls.Remove(ball2);

                        tube.balls.Add(ball2);
                        otherTube.balls.Add(ball1);

                        // Cập nhật vị trí
                        ball1.transform.position = otherTube.GetBallPosition(otherTube.balls.Count - 1);
                        ball2.transform.position = tube.GetBallPosition(tube.balls.Count - 1);

                        break;
                    }
                }
            }
        }
    }

    void Shuffle<T>(List<T> list)
    {
        System.Random rng = new System.Random();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }

    Color GetColorForBallType(int ballType)
    {
        switch (ballType % ballPrefabs.Count)
        {
            case 0: return Color.red;
            case 1: return Color.blue;
            case 2: return Color.green;
            case 3: return Color.yellow;
            default: return Color.white;
        }
    }
    #endregion

    #region Game Logic
    void HandlePlayerInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero);
            
            if (hit.collider != null)
            {
                Tube clickedTube = hit.collider.GetComponent<Tube>();
                if (clickedTube != null) HandleTubeClick(clickedTube);
            }
        }
    }

    void HandleTubeClick(Tube tube)
    {
        if (selectedTube == null)
        {
            if (!tube.IsEmpty)
            {
                selectedTube = tube;
                HighlightTube(tube, true);
            }
        }
        else if (selectedTube == tube)
        {
            HighlightTube(selectedTube, false);
            selectedTube = null;
        }
        else
        {
            if (IsValidMove(selectedTube, tube))
            {
                StartCoroutine(AnimateBallMove(selectedTube, tube));
            }
            HighlightTube(selectedTube, false);
            selectedTube = null;
        }
    }

    IEnumerator AnimateBallMove(Tube fromTube, Tube toTube)
    {
        GameObject ball = fromTube.balls.Last();
        Vector3 startPos = ball.transform.position;
        Vector3 endPos = toTube.GetBallPosition(toTube.balls.Count);
        
        float duration = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            ball.transform.position = Vector3.Lerp(startPos, endPos, elapsed/duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        CompleteMove(fromTube, toTube, ball);
    }

    void CompleteMove(Tube fromTube, Tube toTube, GameObject ball)
    {
        fromTube.balls.Remove(ball);
        toTube.balls.Add(ball);
        ball.transform.SetParent(toTube.transform);
        
        if (CheckWinCondition())
        {
            WinUI.SetActive(true);
        }
    }
    #endregion

    #region Reset System
    void SaveInitialSetup()
    {
        initialSetup = new List<List<Color>>();
        foreach (Tube tube in tubes)
        {
            List<Color> colors = new List<Color>();
            foreach (GameObject ball in tube.balls)
            {
                colors.Add(ball.GetComponent<SpriteRenderer>().color);
            }
            initialSetup.Add(colors);
        }
    }

    public void ResetLevel()
    {
        StartCoroutine(ResetWithAnimation());
        WinUI.SetActive(false);
    }

    IEnumerator ResetWithAnimation() {
        // Hide current balls
        foreach (Tube tube in tubes) {
            foreach (GameObject ball in tube.balls) {
                StartCoroutine(ScaleBall(ball, Vector3.zero, 0.2f));
            }
        }
        
        yield return new WaitForSeconds(0.25f);
        
        // Clear old balls
        foreach (Tube tube in tubes) {
            tube.ClearBalls();
        }
        
        // Recreate balls
        for (int i = 0; i < tubes.Count; i++) {
            Tube tube = tubes[i];
            for (int j = 0; j < initialSetup[i].Count; j++) {
                Color color = initialSetup[i][j];
                GameObject prefab = GetPrefabByColor(color);
                if (prefab != null) {
                    Vector3 pos = tube.GetBallPosition(j);
                    GameObject ball = Instantiate(prefab, pos, Quaternion.identity, tube.transform);
                    ball.transform.localScale = Vector3.zero;
                    StartCoroutine(ScaleBall(ball, Vector3.one, 0.3f));
                    tube.balls.Add(ball);
                }
            }
        }
    }

    IEnumerator ScaleBall(GameObject ball, Vector3 targetScale, float duration) {
        Vector3 startScale = ball.transform.localScale;
        float elapsed = 0f;
        
        while (elapsed < duration) {
            ball.transform.localScale = Vector3.Lerp(startScale, targetScale, elapsed/duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        ball.transform.localScale = targetScale;
    }

    public void PlayAgain(){
        WinUI.SetActive(false);
        GeneratePuzzle();
    }
    #endregion

    #region Helper Methods
    GameObject GetPrefabByColor(Color color)
    {
        return ballPrefabs.FirstOrDefault(prefab => 
            prefab.GetComponent<SpriteRenderer>().color == color);
    }

    bool IsValidMove(Tube from, Tube to)
    {
        if (from.IsEmpty || to.IsFull) return false;
        if (to.IsEmpty) return true;
        return from.TopColor == to.TopColor;
    }

    bool CheckWinCondition()
    {
        foreach (Tube tube in tubes)
        {
            if (!tube.IsEmpty && !tube.IsSameColor()) return false;
        }
        return true;
    }

    void HighlightTube(Tube tube, bool highlight)
    {
        tube.GetComponent<SpriteRenderer>().color = highlight ? Color.yellow : Color.white;
    }
    #endregion
    #region Auto Solver
    private IEnumerator AutoSolveAndGenerate()
    {
        // Tạo bản sao trạng thái hiện tại để thử nghiệm giải
        List<List<Color>> currentState = new List<List<Color>>();
        foreach (Tube tube in tubes)
        {
            List<Color> colors = new List<Color>();
            foreach (GameObject ball in tube.balls)
            {
                colors.Add(ball.GetComponent<SpriteRenderer>().color);
            }
            currentState.Add(colors);
        }

        // Tìm lời giải
        List<Move> solution = FindSolution(currentState);
        
        // Nếu không tìm thấy lời giải, tạo lại màn chơi
        while (solution == null || solution.Count == 0)
        {
            GeneratePuzzle();
            yield return null; // Chờ một frame để tránh đơ máy
            
            // Cập nhật lại currentState
            currentState.Clear();
            foreach (Tube tube in tubes)
            {
                List<Color> colors = new List<Color>();
                foreach (GameObject ball in tube.balls)
                {
                    colors.Add(ball.GetComponent<SpriteRenderer>().color);
                }
                currentState.Add(colors);
            }
            
            solution = FindSolution(currentState);
        }

        // Sau khi tìm thấy lời giải, tiến hành tạo màn chơi
        SaveInitialSetup();
    }

    private class Move
    {
        public int fromTube;
        public int toTube;
        
        public Move(int from, int to)
        {
            fromTube = from;
            toTube = to;
        }
    }

    private List<Move> FindSolution(List<List<Color>> state)
    {
        // Sử dụng BFS để tìm lời giải
        Queue<List<List<Color>>> queue = new Queue<List<List<Color>>>();
        Queue<List<Move>> pathQueue = new Queue<List<Move>>();
        HashSet<string> visited = new HashSet<string>();
        
        // Khởi tạo
        queue.Enqueue(state);
        pathQueue.Enqueue(new List<Move>());
        visited.Add(GetStateHash(state));
        
        while (queue.Count > 0)
        {
            List<List<Color>> current = queue.Dequeue();
            List<Move> currentPath = pathQueue.Dequeue();
            
            // Kiểm tra điều kiện thắng
            if (IsWinState(current))
            {
                return currentPath;
            }
            
            // Tìm tất cả nước đi hợp lệ
            for (int from = 0; from < current.Count; from++)
            {
                if (current[from].Count == 0) continue; // Ống rỗng
                
                for (int to = 0; to < current.Count; to++)
                {
                    if (from == to) continue; // Không di chuyển vào chính nó
                    
                    // Kiểm tra nước đi hợp lệ
                    if (IsValidMove(current, from, to))
                    {
                        // Tạo trạng thái mới
                        List<List<Color>> newState = CloneState(current);
                        Color ball = newState[from].Last();
                        newState[from].RemoveAt(newState[from].Count - 1);
                        newState[to].Add(ball);
                        
                        string hash = GetStateHash(newState);
                        if (!visited.Contains(hash))
                        {
                            visited.Add(hash);
                            List<Move> newPath = new List<Move>(currentPath);
                            newPath.Add(new Move(from, to));
                            queue.Enqueue(newState);
                            pathQueue.Enqueue(newPath);
                        }
                    }
                }
            }
        }
        
        return null; // Không tìm thấy lời giải
    }

    private bool IsValidMove(List<List<Color>> state, int from, int to)
    {
        if (state[from].Count == 0) return false; // Ống nguồn rỗng
        if (state[to].Count >= ballsPerTube) return false; // Ống đích đầy
        
        if (state[to].Count == 0) return true; // Ống đích rỗng
        
        // Kiểm tra màu bóng
        return state[from].Last() == state[to].Last();
    }

    private bool IsWinState(List<List<Color>> state)
    {
        foreach (List<Color> tube in state)
        {
            if (tube.Count == 0) continue;
            if (tube.Count != ballsPerTube) return false;
            
            Color firstColor = tube[0];
            foreach (Color color in tube)
            {
                if (color != firstColor) return false;
            }
        }
        return true;
    }

    private List<List<Color>> CloneState(List<List<Color>> original)
    {
        List<List<Color>> clone = new List<List<Color>>();
        foreach (List<Color> tube in original)
        {
            clone.Add(new List<Color>(tube));
        }
        return clone;
    }

    private string GetStateHash(List<List<Color>> state)
    {
        // Tạo chuỗi hash đại diện cho trạng thái
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (List<Color> tube in state)
        {
            sb.Append("|");
            foreach (Color color in tube)
            {
                sb.Append(color.ToString());
            }
        }
        return sb.ToString();
    }
    #endregion
}