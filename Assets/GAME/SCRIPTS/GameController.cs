using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    [Header("Game Settings")]
    public List<Tube> tubes;
    public List<GameObject> ballPrefabs;
    public Button resetButton;
    public GameObject WinUI;
    public float moveDelayBetweenBalls = 0.1f;

    [Header("Generation Settings")]
    public int tubeCount = 5;
    public int ballsPerTube = 4;
    public int colorCount = 4;
    
    [Header("Animation Settings")]
    public float selectAnimDuration = 0.2f;
    public float moveToTubeDuration = 0.3f;
    public float moveIntoTubeDuration = 0.2f;
    
    [Header("Screen Settings")]
    public ScreenManager screenManager;

    private GameObject selectedBall = null;
    private Vector3 selectedBallOriginalPos;

    private Tube selectedTube = null;
    private List<List<Color>> initialSetup;
    private List<MovingBall> activeMoves = new List<MovingBall>();
    private Tube pendingFromTube = null;
    private List<GameObject> pendingBalls = new List<GameObject>();
    private List<(Tube fromTube, Tube toTube, List<GameObject> balls)> pendingMoves = new List<(Tube, Tube, List<GameObject>)>();

    void Start()
    {
        resetButton.onClick.AddListener(ResetLevel);    //Gán sự kiện click nút reset
        GeneratePuzzle();   //tạo màn chơi
    }

    void Update()
    {
        UpdateActiveMoves();
        HandlePlayerInput();    //xử lý input của người chơi
    }

    #region Level Generation
    void GeneratePuzzle()
    {
        // Xóa bóng trong các ống hiện có
        foreach (Tube tube in tubes)
        {
            tube.ClearBalls();
        }

        // Tạo và xáo trộn bóng
        List<GameObject> allBalls = CreateAndShuffleBalls();
        
        // Phân bố bóng vào các ống
        DistributeBalls(allBalls);
        
        EnsureSolvable();   //không để game dễ QUÁ
        StartCoroutine(AutoSolveAndGenerate()); //tự động giả và kiểm tra màn chơi
        SaveInitialSetup(); //lưu trạng thái để reset
        // Sau khi tạo xong các ống
        if (screenManager != null)
            screenManager.AdjustCameraAndLayout();
    }

    List<GameObject> CreateAndShuffleBalls()
    {
        List<GameObject> allBalls = new List<GameObject>();
        //tạo bóng với mỗi màu
        for (int colorIndex = 0; colorIndex < colorCount; colorIndex++)
        {
            GameObject prefab = ballPrefabs[colorIndex % ballPrefabs.Count];
            for (int i = 0; i < ballsPerTube; i++)
            {
                allBalls.Add(prefab);
            }
        }
        Shuffle(allBalls);  //trộn bóng
        return allBalls;
    }

    void DistributeBalls(List<GameObject> allBalls)
    {
        for (int tubeIndex = 0; tubeIndex < tubes.Count; tubeIndex++)
        {
            if (tubeIndex < colorCount) //số ống phân bố chỉ được banwgf số màu
            {
                for (int ballIndex = 0; ballIndex < ballsPerTube; ballIndex++)
                {
                    int listIndex = tubeIndex * ballsPerTube + ballIndex;
                    if (listIndex < allBalls.Count)
                    {
                        CreateBallInTube(tubes[tubeIndex], allBalls[listIndex], ballIndex);
                    }
                }
            }
        }
    }
    
    //tạo bóng
    void CreateBallInTube(Tube tube, GameObject prefab, int ballIndex)
    {
        Vector3 pos = tube.GetBallPosition(ballIndex);  //lấy vị trí
        GameObject ball = Instantiate(prefab, pos, Quaternion.identity, tube.transform);    //tạo bóng
        tube.balls.Add(ball);
    }

    void EnsureSolvable()
    {
        foreach (Tube tube in tubes)
        {
            if (tube.IsSameColor() && tube.balls.Count == ballsPerTube) //nếu ống đã hoàn thành
            {
                SwapTopBallWithOtherTube(tube); //đổi bóng trên cùng với ống khác
                break;
            }
        }
    }

    void SwapTopBallWithOtherTube(Tube tube)
    {
        foreach (Tube otherTube in tubes)
        {
            if (otherTube != tube && !otherTube.IsEmpty)
            {
                //lấy bóng
                GameObject ball1 = tube.balls.Last();
                GameObject ball2 = otherTube.balls.Last();
                
                //đổi vị trí trong list
                tube.balls.Remove(ball1);
                otherTube.balls.Remove(ball2);

                tube.balls.Add(ball2);
                otherTube.balls.Add(ball1);
                
                //update vị trí thực
                ball1.transform.position = otherTube.GetBallPosition(otherTube.balls.Count - 1);
                ball2.transform.position = tube.GetBallPosition(tube.balls.Count - 1);
                break;
            }
        }
    }
    
    //hàm trộn bóng
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
    #endregion

    #region Game Logic
    void HandlePlayerInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            //raycast phát hiện click
            RaycastHit2D hit = Physics2D.Raycast(
                Camera.main.ScreenToWorldPoint(Input.mousePosition), 
                Vector2.zero);
            
            //nếu click vào ống
            if (hit.collider != null && hit.collider.TryGetComponent<Tube>(out Tube clickedTube))
            {
                HandleTubeClick(clickedTube);
            }
        }
    }

    //xử lý click vào ống
    void HandleTubeClick(Tube tube)
    {
        if (selectedTube == null)
        {
            if (GetAvailableBallsCount(tube) > 0)
            {
                selectedTube = tube;
                selectedBall = tube.balls.Last();
                selectedBallOriginalPos = selectedBall.transform.position;
                AddMoveAnimation(selectedBall, tube.GetTopPosition(), selectAnimDuration);
            }
        }
        else if (selectedTube == tube)
        {
            AddMoveAnimation(selectedBall, selectedBallOriginalPos, selectAnimDuration);
            selectedTube = null;
            selectedBall = null;
        }
        else
        {
            if (IsValidMove(selectedTube, tube))
            {
                List<GameObject> ballsToMove = GetMovableBalls(selectedTube, tube);
                StartCoroutine(ExecuteMove(selectedTube, tube, ballsToMove));
            }
            else
            {
                AddMoveAnimation(selectedBall, selectedBallOriginalPos, selectAnimDuration);
            }
            selectedTube = null;
            selectedBall = null;
        }
    }
    bool IsBallMovingFromTube(Tube tube)
    {
        foreach (var move in activeMoves)
        {
            if (tube.balls.Contains(move.ball))
                return true;
        }
        return false;
    }
    void AddMoveAnimation(GameObject ball, Vector3 targetPos, float duration)
    {
        activeMoves.Add(new MovingBall(ball, targetPos, duration));
    }
    
    IEnumerator ExecuteMove(Tube fromTube, Tube toTube, List<GameObject> ballsToMove)
    {
        // Thêm vào danh sách pending moves
        pendingMoves.Add((fromTube, toTube, ballsToMove));
    
        foreach (GameObject ball in ballsToMove)
        {
            // 1. Cập nhật trạng thái NGAY LẬP TỨC
            fromTube.balls.Remove(ball);
            toTube.balls.Add(ball);
            ball.transform.SetParent(toTube.transform);
        
            // 2. Bay lên miệng ống nguồn
            Vector3 tubeTopPos = fromTube.GetTopPosition();
            AddMoveAnimation(ball, tubeTopPos, selectAnimDuration);
        
            yield return new WaitUntil(() => activeMoves.Find(m => m.ball == ball) == null);
        
            // 3. Di chuyển sang miệng ống đích
            Vector3 targetTopPos = toTube.GetTopPosition();
            AddMoveAnimation(ball, targetTopPos, moveToTubeDuration);
        
            yield return new WaitUntil(() => activeMoves.Find(m => m.ball == ball) == null);
        
            // 4. Rơi vào vị trí trong ống đích
            Vector3 finalPos = toTube.GetBallPosition(toTube.balls.Count - 1);
            AddMoveAnimation(ball, finalPos, moveIntoTubeDuration);
        
            if (ball != ballsToMove.Last())
                yield return new WaitForSeconds(moveDelayBetweenBalls);
        }
    
        yield return new WaitUntil(() => activeMoves.Count == 0);
    
        // Xóa khỏi danh sách pending moves khi hoàn thành
        pendingMoves.RemoveAll(m => m.fromTube == fromTube && m.toTube == toTube);
    
        if (CheckWinCondition())
            WinUI.SetActive(true);
    }
    
    List<GameObject> GetMovableBalls(Tube fromTube, Tube toTube)
    {
        List<GameObject> movableBalls = new List<GameObject>();
        if (fromTube.IsEmpty) return movableBalls;

        // Lấy tất cả bóng cùng màu liên tiếp từ trên xuống
        List<GameObject> sameColorBalls = fromTube.GetTopSameColorBalls();
    
        // Tính số bóng có thể di chuyển (không vượt quá chỗ trống)
        int availableSpace = toTube.GetAvailableSpace();
        int moveCount = Mathf.Min(sameColorBalls.Count, availableSpace);
    
        // Lấy số bóng sẽ di chuyển (từ trên xuống)
        for (int i = 0; i < moveCount; i++)
        {
            movableBalls.Add(sameColorBalls[i]);
        }
    
        return movableBalls;
    }

    void UpdateActiveMoves()
    {
        for (int i = activeMoves.Count - 1; i >= 0; i--)
        {
            // Kiểm tra nếu bóng đã bị destroy
            if (activeMoves[i].ball == null)
            {
                activeMoves.RemoveAt(i);
                continue;
            }
        
            if (activeMoves[i].Update())
            {
                activeMoves.RemoveAt(i);
            }
        }
    }
    #endregion

    #region Reset System
    void SaveInitialSetup() //lưu trạng tái ban đầu
    {
        initialSetup = tubes.ConvertAll(tube => 
            tube.balls.ConvertAll(ball => ball.GetComponent<SpriteRenderer>().color));
    }

    public void ResetLevel()
    {
        StartCoroutine(ResetWithAnimation());
        WinUI.SetActive(false);
    }

    IEnumerator ResetWithAnimation() 
    {
        // Dừng tất cả di chuyển đang hoạt động
        activeMoves.Clear();
        pendingMoves.Clear();
    
        // Ẩn các bóng hiện tại
        foreach (Tube tube in tubes) 
        {
            foreach (GameObject ball in tube.balls.ToList()) // Sử dụng ToList() để tránh modify collection while iterating
            {
                if (ball != null)
                {
                    yield return StartCoroutine(ScaleBall(ball, Vector3.zero, 0.2f));
                    Destroy(ball);
                }
            }
            tube.balls.Clear();
        }
    
        yield return new WaitForSeconds(0.25f);
    
        // Tạo lại bóng từ initial setup
        for (int i = 0; i < tubes.Count; i++) 
        {
            Tube tube = tubes[i];
            for (int j = 0; j < initialSetup[i].Count; j++) 
            {
                Color color = initialSetup[i][j];
                GameObject prefab = GetPrefabByColor(color);
                if (prefab != null) 
                {
                    CreateBallWithAnimation(tube, prefab, j);
                    yield return new WaitForSeconds(0.05f); // Thêm delay nhỏ giữa các bóng
                }
            }
        }
    }

    void CreateBallWithAnimation(Tube tube, GameObject prefab, int positionIndex)
    {
        Vector3 pos = tube.GetBallPosition(positionIndex);
        GameObject ball = Instantiate(prefab, pos, Quaternion.identity, tube.transform);
        ball.transform.localScale = Vector3.zero;
        StartCoroutine(ScaleBall(ball, Vector3.one, 0.3f));
        tube.balls.Add(ball);
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
        // Dừng tất cả coroutine đang chạy
        StopAllCoroutines();
    
        // Xóa tất cả các bóng di chuyển đang hoạt động
        activeMoves.Clear();
        pendingMoves.Clear();
    
        // Tắt UI chiến thắng
        WinUI.SetActive(false);
    
        // Tạo lại puzzle
        GeneratePuzzle();
    }
    #endregion

    #region Helper Methods
    GameObject GetPrefabByColor(Color color)    //lấy prefab theo màu
    {
        return ballPrefabs.FirstOrDefault(prefab => 
            prefab.GetComponent<SpriteRenderer>().color.Equals(color));
    }

    bool IsValidMove(Tube fromTube, Tube toTube)
    {
        // Không cho phép di chuyển nếu có pending move giữa hai ống này
        if (pendingMoves.Any(m => m.fromTube == fromTube && m.toTube == toTube))
            return false;

        // Lấy trạng thái "sẽ có" của các ống
        Color fromColor = GetActualTopColor(fromTube);
        Color toColor = GetActualTopColor(toTube);

        // Kiểm tra điều kiện hợp lệ
        if (fromColor == Color.clear) // fromTube sẽ rỗng
            return false;
        
        if (toTube.GetAvailableSpace() <= 0 && toColor != fromColor) // toTube sẽ đầy
            return false;
        
        if (toColor == Color.clear) // toTube sẽ rỗng
            return true;
        
        return fromColor.Equals(toColor);
    }
    int GetAvailableBallsCount(Tube tube)
    {
        int movingBalls = pendingMoves
            .Where(m => m.fromTube == tube)
            .Sum(m => m.balls.Count);
        
        return tube.balls.Count - movingBalls;
    }

    bool IsAnyBallMovingBetweenTubes(Tube fromTube, Tube toTube)
    {
        foreach (var move in activeMoves)
        {
            // Nếu có bóng đang di chuyển từ ống nguồn đến ống đích
            if (fromTube.balls.Contains(move.ball))
            {
                // Kiểm tra xem bóng này có đang di chuyển đến toTube không
                Tube targetTube = GetTubeFromPosition(move.targetPosition);
                if (targetTube == toTube)
                    return true;
            }
        }
        return false;
    }

    Color GetActualTopColor(Tube tube)
    {
        // Kiểm tra pending moves đầu tiên
        foreach (var move in pendingMoves)
        {
            // Nếu có bóng sắp di chuyển đi từ ống này
            if (move.fromTube == tube)
            {
                // Nếu đây là bóng trên cùng
                if (tube.balls.Count > 0 && move.balls.Contains(tube.balls.Last()))
                {
                    // Trả về màu của bóng tiếp theo (nếu có)
                    if (tube.balls.Count > move.balls.Count)
                        return tube.balls[tube.balls.Count - move.balls.Count - 1].GetComponent<SpriteRenderer>().color;
                    else
                        return Color.clear; // Ống sẽ rỗng
                }
            }
        
            // Nếu có bóng sắp di chuyển đến ống này
            if (move.toTube == tube)
            {
                return move.balls[0].GetComponent<SpriteRenderer>().color;
            }
        }
    
        // Nếu không có pending moves, trả về màu hiện tại
        return tube.TopColor;
    }
    Tube GetTubeFromPosition(Vector3 position)
    {
        foreach (Tube tube in tubes)
        {
            if (Vector3.Distance(position, tube.GetTopPosition()) < 0.1f || 
                (tube.balls.Count > 0 && Vector3.Distance(position, tube.GetBallPosition(tube.balls.Count - 1)) < 0.1f))
            {
                return tube;
            }
        }
        return null;
    }
    bool CheckWinCondition()
    {
        return tubes.All(tube => tube.IsEmpty || tube.IsSameColor());
    }
    #endregion

    #region Auto Solver
    private IEnumerator AutoSolveAndGenerate()  //tự động giaỉ và tạo lại
    {
        List<List<Color>> currentState = GetCurrentState(); //lấy trạng thái hiện tại
        List<Move> solution = FindSolution(currentState);   //thử giải
        
        while (solution == null || solution.Count == 0) //tạo lại nếu ko giải được
        {
            GeneratePuzzle();
            yield return null;
            currentState = GetCurrentState();
            solution = FindSolution(currentState);
        }

        SaveInitialSetup(); //lưu trạng thái
    }

    private List<List<Color>> GetCurrentState()
    {
        return tubes.ConvertAll(tube => 
            tube.balls.ConvertAll(ball => ball.GetComponent<SpriteRenderer>().color));
    }

    //thử giải bằng BFS
    private List<Move> FindSolution(List<List<Color>> state)
    {
        Queue<(List<List<Color>> state, List<Move> path)> queue = new Queue<(List<List<Color>>, List<Move>)>();
        HashSet<string> visited = new HashSet<string>();
        
        //khởi tạo
        queue.Enqueue((state, new List<Move>()));
        visited.Add(GetStateHash(state));
        
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            
            if (IsWinState(current.state))
            {
                return current.path;
            }
            
            // Duyệt tất cả các nước đi có thể
            for (int from = 0; from < current.state.Count; from++)
            {
                if (current.state[from].Count == 0) continue;
                
                for (int to = 0; to < current.state.Count; to++)
                {
                    if (from == to) continue;
                    
                    // Nếu nước đi hợp lệ
                    if (IsValidMove(current.state, from, to))
                    {
                        // Tạo trạng thái mới
                        List<List<Color>> newState = CloneState(current.state);
                        newState[to].Add(newState[from].Last());
                        newState[from].RemoveAt(newState[from].Count - 1);
                        
                        string hash = GetStateHash(newState);
                        if (!visited.Contains(hash))
                        {
                            visited.Add(hash);
                            List<Move> newPath = new List<Move>(current.path);
                            newPath.Add(new Move(from, to));
                            queue.Enqueue((newState, newPath));
                        }
                    }
                }
            }
        }
        return null;
    }

    private bool IsValidMove(List<List<Color>> state, int from, int to)
    {
        if (state[from].Count == 0 || state[to].Count >= ballsPerTube) return false;
        return state[to].Count == 0 || state[from].Last().Equals(state[to].Last());
    }

    private bool IsWinState(List<List<Color>> state)
    {
        return state.All(tube => tube.Count == 0 || (tube.Count == ballsPerTube && tube.All(c => c.Equals(tube[0]))));
    }

    private List<List<Color>> CloneState(List<List<Color>> original)    // Sao chép trạng thái
    {
        return original.ConvertAll(tube => new List<Color>(tube));
    }

    private string GetStateHash(List<List<Color>> state)    // Tạo hash cho trạng thái
    {
        return string.Join("|", state.ConvertAll(tube => string.Join(",", tube.ConvertAll(c => c.ToString()))));
    }

    private class Move  // Lớp đại diện cho một nước đi
    {
        public int fromTube;
        public int toTube;
        
        public Move(int from, int to)
        {
            fromTube = from;
            toTube = to;
        }
    }
    #endregion
}
public class MovingBall
{
    public GameObject ball;
    public Vector3 targetPosition;
    public float duration;
    public float elapsed;
    public bool isCompleted;
    
    public MovingBall(GameObject ball, Vector3 target, float duration)
    {
        this.ball = ball;
        this.targetPosition = target;
        this.duration = duration;
        this.elapsed = 0f;
        this.isCompleted = false;
    }
    
    public bool Update()
    {
        if (isCompleted || ball == null) return true;
        
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        ball.transform.position = Vector3.Lerp(ball.transform.position, targetPosition, t);
        
        if (elapsed >= duration)
        {
            ball.transform.position = targetPosition;
            isCompleted = true;
            return true;
        }
        return false;
    }
}