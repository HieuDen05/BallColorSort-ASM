using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Tube : MonoBehaviour
{
    public List<GameObject> balls = new List<GameObject>();
    public const int ballsPerTube = 4;

    public bool IsEmpty => balls.Count == 0;
    public bool IsFull => balls.Count >= 4;
    public Color TopColor => IsEmpty ? Color.clear : balls.Last().GetComponent<SpriteRenderer>().color;

    public Vector3 GetBallPosition(int ballIndex)
    {
        float spacing = 1.1f; // Khoảng cách giữa các bóng
        float baseHeight = -1.65f; // Vị trí đáy ống
        return transform.position + new Vector3(0, baseHeight, 0) + Vector3.up * (ballIndex * spacing);
    }

    public bool IsSameColor()
    {
        if (IsEmpty || balls.Count < 4) return false;
        Color firstColor = balls[0].GetComponent<SpriteRenderer>().color;
        return balls.All(ball => ball.GetComponent<SpriteRenderer>().color == firstColor);
    }

    public void ClearBalls()
    {
        foreach (GameObject ball in balls)
        {
            if (ball != null) Destroy(ball);
        }
        balls.Clear();
    }

    public Vector3 GetTopPosition()
    {
        return transform.position + new Vector3(0, 2.85f, 0);
    }
    public List<GameObject> GetTopSameColorBalls()
    {
        List<GameObject> sameColorBalls = new List<GameObject>();
        if (IsEmpty) return sameColorBalls;

        Color topColor = TopColor;
        // Duyệt từ trên xuống dưới
        for (int i = balls.Count - 1; i >= 0; i--)
        {
            if (balls[i].GetComponent<SpriteRenderer>().color.Equals(topColor))
            {
                sameColorBalls.Add(balls[i]);
            }
            else
            {
                break;
            }
        }
        return sameColorBalls;
    }

    public int GetAvailableSpace()
    {
        return ballsPerTube - balls.Count;
    }
}