using UnityEngine;

/// <summary>
/// Single cell in the Game of Life grid: visual and collider. Collider is enabled only when alive so you can collide with live cells.
/// </summary>
public class GameOfLifeCellView : MonoBehaviour
{
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;

    public void SetReferences(SpriteRenderer sr, Collider2D col)
    {
        _spriteRenderer = sr;
        _collider = col;
    }

    public void SetAlive(bool alive, Color aliveColor, Color deadColor)
    {
        if (_spriteRenderer != null)
            _spriteRenderer.color = alive ? aliveColor : deadColor;
        if (_collider != null)
            _collider.enabled = alive;
    }
}
