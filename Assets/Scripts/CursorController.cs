using System.Collections;
using UnityEngine;

/// <summary>
/// Mouse follows the hardware cursor in world space (exact screen mapping). Click to begin play.
/// Easy mode: first live hit — pause at impact with pulse; then snap in-game cursor to the mouse,
/// resume sim, iframes with sprite flash while the OS cursor stays visible and unlocked; then hide and lock again.
/// Hard mode: 1 life.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D), typeof(Rigidbody2D))]
public class CursorController : MonoBehaviour
{
    [Header("Behavior")]
    [SerializeField] private bool followMouse = true;

    [SerializeField] private GameOfLifeSimulation simulation;
    [SerializeField] private Vector2[] _clickColliderPoints;
    [SerializeField] private Vector2[] _playColliderPoints;

    [Header("Invulnerability (easy mode)")]
    [SerializeField] private float iframeFlashInterval = 0.08f;

    private LevelManager _levelManager;

    private bool _hasCollided;
    private bool _waitingForStart;
    private Rigidbody2D _rb;
    private PolygonCollider2D _collider;
    private SpriteRenderer _spriteRenderer;
    private Color _spriteBaseColor;
    private int _livesRemaining;
    private float _invulnerableUntil;
    private Coroutine _flashRoutine;
    private Coroutine _easyHitRoutine;
    private bool _inEasyHitRecovery;
    private bool _isGameStarted;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;

        _collider = GetComponent<PolygonCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _spriteBaseColor = _spriteRenderer.color;

        _levelManager = FindFirstObjectByType<LevelManager>();
        if (simulation == null)
            simulation = FindFirstObjectByType<GameOfLifeSimulation>();

        if (_levelManager != null)
            _levelManager.OnLevelLoaded += OnLevelLoaded;
    }

    private void OnDestroy()
    {
        if (_levelManager != null)
            _levelManager.OnLevelLoaded -= OnLevelLoaded;
    }

    private void OnLevelLoaded(int index)
    {
        ApplyColliderPoints(_clickColliderPoints);
        _isGameStarted = false;
    }

    private void ApplyColliderPoints(Vector2[] points)
    {
        if (_collider == null || points == null || points.Length < 3) return;
        _collider.points = points;
    }

    /// <summary>Reset lives from difficulty; call when a level loads without PlaceAtStart.</summary>
    public void PrepareForNewLevel()
    {
        StopFlash();
        if (_easyHitRoutine != null)
        {
            StopCoroutine(_easyHitRoutine);
            _easyHitRoutine = null;
        }
        _inEasyHitRecovery = false;
        _livesRemaining = GameDifficulty.MaxLives;
        _invulnerableUntil = 0f;
        if (_spriteRenderer != null)
            _spriteRenderer.color = _spriteBaseColor;
    }

    private void Update()
    {
        if (!_hasCollided && followMouse)
            ApplyMouseWorldPosition();
    }

    private void OnMouseDown()
    {
        if (!_isGameStarted)
            StartGame();
    }

    private Vector3 MouseWorldOnPlane()
    {
        var cam = Camera.main;
        if (cam == null) return transform.position;
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        return cam.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, depth));
    }

    private void ApplyMouseWorldPosition()
    {
        Vector3 w = MouseWorldOnPlane();
        Vector3 pos = transform.position;
        pos.x = w.x;
        pos.y = w.y;
        transform.position = pos;
    }

    private bool IsInvulnerable()
    {
        return Time.time < _invulnerableUntil;
    }

    private bool IsLiveCellCollider(Collider2D col)
    {
        if (col == null) return false;
        if (_inEasyHitRecovery) return false;
        if (simulation != null && !simulation.IsRunning) return false;
        if (IsInvulnerable()) return false;
        return col.GetComponent<GameOfLifeCellView>() != null && col.enabled;
    }

    private void TryHitLiveCell(Vector2 hitWorldPosition)
    {
        AudioManager.Instance.PlayHitSound();
        if (_livesRemaining > 1 && !_inEasyHitRecovery)
        {
            _livesRemaining--;
            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);
            if (_easyHitRoutine != null)
                StopCoroutine(_easyHitRoutine);
            _easyHitRoutine = StartCoroutine(EasyHitRecoveryRoutine(hitWorldPosition));
            return;
        }

        HandleDeath();
    }

    private IEnumerator EasyHitRecoveryRoutine(Vector2 hitWorld)
    {
        _inEasyHitRecovery = true;
        if (simulation != null)
            simulation.StopSimulation();

        followMouse = false;

        float z = transform.position.z;
        transform.position = new Vector3(hitWorld.x, hitWorld.y, z);

        Cursor.visible = true;

        float pause = GameDifficulty.EasyHitPauseSeconds;
        float elapsed = 0f;
        while (elapsed < pause)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_spriteRenderer != null)
            {
                float pulse = Mathf.Sin(elapsed * 30f) * 0.5f + 0.5f;
                var c = _spriteBaseColor;
                c.a = Mathf.Lerp(0.2f, 1f, pulse);
                _spriteRenderer.color = c;
            }
            yield return null;
        }

        if (_spriteRenderer != null)
            _spriteRenderer.color = _spriteBaseColor;

        if (simulation != null)
            simulation.ResumeSimulationAfterHit();

        // Snap in-game cursor to the hardware cursor in world space.
        Vector3 mw = MouseWorldOnPlane();
        transform.position = new Vector3(mw.x, mw.y, transform.position.z);

        _invulnerableUntil = Time.time + GameDifficulty.EasyHitIframesAfterPauseSeconds;
        followMouse = true;

        Cursor.visible = true;

        _flashRoutine = StartCoroutine(FlashWhileInvulnerable());

        _easyHitRoutine = null;
        _inEasyHitRecovery = false;
    }

    private IEnumerator FlashWhileInvulnerable()
    {
        var sr = _spriteRenderer;
        if (sr == null) yield break;

        while (Time.time < _invulnerableUntil)
        {
            var c = sr.color;
            c.a = c.a > 0.5f ? 0.2f : 1f;
            sr.color = c;
            yield return new WaitForSeconds(iframeFlashInterval);
        }

        sr.color = _spriteBaseColor;
        _flashRoutine = null;

        Cursor.visible = false;
    }

    private void StopFlash()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }
        if (_spriteRenderer != null)
            _spriteRenderer.color = _spriteBaseColor;

        Cursor.visible = false;
    }

    private void HandleDeath()
    {
        if (_hasCollided) return;
        _hasCollided = true;
        StopFlash();

        followMouse = false;
        Cursor.visible = true;

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = true;

        if (simulation != null)
            simulation.OnPlayerDied();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsLiveCellCollider(collision.collider)) return;
        Vector2 hit = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.collider.ClosestPoint(transform.position);
        TryHitLiveCell(hit);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsLiveCellCollider(other)) return;
        Vector2 hit = other.ClosestPoint(transform.position);
        TryHitLiveCell(hit);
    }

    public void PlaceAtStart(Vector2 worldPosition)
    {
        _hasCollided = false;
        _waitingForStart = true;
        followMouse = false;

        PrepareForNewLevel();

        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

        Cursor.visible = true;

        if (_spriteRenderer != null)
            _spriteRenderer.enabled = true;
    }

    private void StartGame()
    {
        if (!_waitingForStart) return;
        _waitingForStart = false;
        _isGameStarted = true;

        followMouse = true;
        ApplyMouseWorldPosition();
        Cursor.visible = false;
        ApplyColliderPoints(_playColliderPoints);

        if (simulation != null)
            simulation.StartSimulation();
    }
}
