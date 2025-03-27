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

    [Header("Generation Settings")]
    public int tubeCount = 5;
    public int ballsPerTube = 4;
    public int colorCount = 4;
    
    [Header("Animation Settings")]
    public float selectAnimDuration = 0.2f;
    public float moveToTubeDuration = 0.3f;
    public float moveIntoTubeDuration = 0.2f;

    private GameObject selectedBall = null;
    private Vector3 selectedBallOriginalPos;

    private Tube selectedTube = null;
    private List<List<Color>> initialSetup;

    void Start()
    {
        resetButton.onClick.AddListener(ResetLevel);    //Gán sự kiện click nút reset
        GeneratePuzzle();   //tạo màn chơi
    }

    void Update()
    {
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
            if (!tube.IsEmpty)
            {
                selectedTube = tube;
                selectedBall = tube.balls.Last();
                selectedBallOriginalPos = selectedBall.transform.position;
                //animation chọn bóng
                StartCoroutine(AnimateBallSelect(selectedBall, tube.GetTopPosition()));
            }
        }
        else if (selectedTube == tube)
        {
            // Bỏ chọn - đưa bóng về vị trí cũ
            StartCoroutine(AnimateBallDeselect(selectedBall, selectedBallOriginalPos));
            selectedTube = null;
            selectedBall = null;
        }
        else
        {
            // Nếu di chuyển hợp lệ
            if (IsValidMove(selectedTube, tube))
            {
                // Lấy danh sách bóng có thể di chuyển
                List<GameObject> ballsToMove = GetMovableBalls(selectedTube, tube);
                // Animation di chuyển nhiều bóng
                StartCoroutine(AnimateMultipleBallsMove(selectedTube, tube, ballsToMove));
            }
            else
            {
                // Nếu không hợp lệ, bỏ chọn
                StartCoroutine(AnimateBallDeselect(selectedBall, selectedBallOriginalPos));
            }
            selectedTube = null;
            selectedBall = null;
        }
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

    IEnumerator AnimateMultipleBallsMove(Tube fromTube, Tube toTube, List<GameObject> ballsToMove)
    {
        // Bóng đầu tiên (đang ở miệng ống)
        GameObject firstBall = ballsToMove[0];
        Vector3 firstBallEndPos = toTube.GetBallPosition(toTube.balls.Count);
        
        // 1. Di chuyển bóng đầu tiên sang ống đích
        yield return StartCoroutine(MoveBallToNewTube(firstBall, toTube.GetTopPosition(), firstBallEndPos));
        
        // Cập nhật trạng thái ngay để tính toán vị trí chính xác
        fromTube.balls.Remove(firstBall);
        toTube.balls.Add(firstBall);
        firstBall.transform.SetParent(toTube.transform);

        // 2. Di chuyển các bóng tiếp theo (nối đuôi)
        for (int i = 1; i < ballsToMove.Count; i++)
        {
            GameObject ball = ballsToMove[i];
            Vector3 endPos = toTube.GetBallPosition(toTube.balls.Count);
            
            // 2a. Bay lên miệng ống nguồn
            yield return StartCoroutine(MoveBallToPosition(
                ball,
                fromTube.GetTopPosition(),
                selectAnimDuration));
            
            // 2b. Di chuyển sang ống đích
            yield return StartCoroutine(MoveBallToNewTube(
                ball,
                toTube.GetTopPosition(),
                endPos));
            
            // Cập nhật trạng thái
            fromTube.balls.Remove(ball);
            toTube.balls.Add(ball);
            ball.transform.SetParent(toTube.transform);
        }

        if (CheckWinCondition())
            WinUI.SetActive(true);
    }

    IEnumerator MoveBallToPosition(GameObject ball, Vector3 targetPos, float duration)
    {
        Vector3 startPos = ball.transform.position;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            ball.transform.position = Vector3.Lerp(startPos, targetPos, elapsed/duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        ball.transform.position = targetPos;
    }

    IEnumerator MoveBallToNewTube(GameObject ball, Vector3 tubeTopPos, Vector3 finalPos)
    {
        // Di chuyển đến miệng ống đích
        yield return StartCoroutine(MoveBallToPosition(ball, tubeTopPos, moveToTubeDuration));
        
        // Di chuyển vào vị trí cuối cùng
        yield return StartCoroutine(MoveBallToPosition(ball, finalPos, moveIntoTubeDuration));
    }
    
    IEnumerator AnimateBallSelect(GameObject ball, Vector3 targetPos)
    {
        float elapsed = 0f;
        Vector3 startPos = ball.transform.position;
    
        while (elapsed < selectAnimDuration)
        {
            ball.transform.position = Vector3.Lerp(startPos, targetPos, elapsed/selectAnimDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
    
        ball.transform.position = targetPos;
    }

    IEnumerator AnimateBallDeselect(GameObject ball, Vector3 targetPos)
    {
        float elapsed = 0f;
        Vector3 startPos = ball.transform.position;
    
        while (elapsed < selectAnimDuration)
        {
            ball.transform.position = Vector3.Lerp(startPos, targetPos, elapsed/selectAnimDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }
    
        ball.transform.position = targetPos;
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

    IEnumerator ResetWithAnimation() {
        // Ẩn các bóng hiện tại
        foreach (Tube tube in tubes) {
            foreach (GameObject ball in tube.balls) {
                StartCoroutine(ScaleBall(ball, Vector3.zero, 0.2f));
            }
        }
        
        yield return new WaitForSeconds(0.25f);
        
        // Xóa và tạo lại bóng
        foreach (Tube tube in tubes) {
            tube.ClearBalls();
        }
        
        for (int i = 0; i < tubes.Count; i++) {
            Tube tube = tubes[i];
            for (int j = 0; j < initialSetup[i].Count; j++) {
                Color color = initialSetup[i][j];
                GameObject prefab = GetPrefabByColor(color);
                if (prefab != null) {
                    CreateBallWithAnimation(tube, prefab, j);
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
        WinUI.SetActive(false);
        GeneratePuzzle();
    }
    #endregion

    #region Helper Methods
    GameObject GetPrefabByColor(Color color)    //lấy prefab theo màu
    {
        return ballPrefabs.FirstOrDefault(prefab => 
            prefab.GetComponent<SpriteRenderer>().color.Equals(color));
    }

    bool IsValidMove(Tube from, Tube to)
    {
        if (from.IsEmpty || to.IsFull) return false;
    
        // Nếu ống đích rỗng, có thể di chuyển bất kỳ bóng nào
        if (to.IsEmpty) return true;
    
        // Chỉ di chuyển được nếu màu trùng với bóng trên cùng ống đích
        return from.TopColor.Equals(to.TopColor);
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