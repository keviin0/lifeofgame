using UnityEngine;

/// <summary>
/// After the level starts, moves the sprite by the mouse's screen delta each frame
/// (converted to world space) so it does not snap to the cursor on click.
/// Stops when it collides with a live cell. Before start, placed at cursor start and clicked to begin.
/// Before the level starts, it can be placed at a \"cursor start\" cell and
/// clicked to begin the simulation.
/// Requires a 2D collider and a Rigidbody2D on this object.
/// </summary>
[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class CursorController : MonoBehaviour
{
    [Header("Behavior")]
    [Tooltip("If true, the sprite follows the mouse (used after start).")]
    [SerializeField] private bool followMouse = true;

    [Tooltip("Simulation to start when this sprite is clicked at the beginning of a level.")]
    [SerializeField] private GameOfLifeSimulation simulation;
    [SerializeField] private Vector2[] _clickColliderPoints;
    [SerializeField] private Vector2[] _playColliderPoints;
    private LevelManager _levelManager;

    private bool _hasCollided;
    private bool _waitingForStart;
    private Vector3 _lastMouseScreen;
    private Rigidbody2D _rb;
    private PolygonCollider2D _collider;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;

        _collider = GetComponent<PolygonCollider2D>();
        if (_levelManager == null)
            _levelManager = FindFirstObjectByType<LevelManager>();
        if (simulation == null)
            simulation = FindFirstObjectByType<GameOfLifeSimulation>();

        _levelManager.OnLevelLoaded += OnLevelLoaded;
    }

    private void OnLevelLoaded(int index)
    {
        _collider.points = _clickColliderPoints;
    }

    private void Update()
    {
        if (!_hasCollided && followMouse)
            ApplyMouseDelta();
    }

    /// <summary>
    /// Applies movement from screen-space mouse delta so the triangle stays under the click
    /// until the pointer actually moves (no snap to absolute mouse world position).
    /// </summary>
    private void ApplyMouseDelta()
    {
        var cam = Camera.main;
        if (cam == null) return;

        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        Vector3 current = Input.mousePosition;

        Vector3 curWorld = cam.ScreenToWorldPoint(new Vector3(current.x, current.y, depth));
        Vector3 lastWorld = cam.ScreenToWorldPoint(new Vector3(_lastMouseScreen.x, _lastMouseScreen.y, depth));

        Vector3 delta = curWorld - lastWorld;
        _lastMouseScreen = current;

        Vector3 pos = transform.position;
        pos.x += delta.x;
        pos.y += delta.y;
        transform.position = pos;
    }

    private bool IsLiveCellCollider(Collider2D col)
    {
        if (col == null) return false;
        // Only stop when hitting a GameOfLifeCellView collider (live cells enable their collider)
        // while the simulation is actually running.
        if (simulation != null && !simulation.IsRunning) return false;
        return col.GetComponent<GameOfLifeCellView>() != null && col.enabled;
    }

    private void HandleCollisionStop()
    {
        if (_hasCollided) return;
        _hasCollided = true;

        // Stop following the mouse.
        followMouse = false;

        // Show the OS cursor and ensure this sprite is visible.
        Cursor.visible = true;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
        }

        // Trigger a death transition + reload on the simulation.
        if (simulation != null)
            simulation.OnPlayerDied();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsLiveCellCollider(collision.collider))
            HandleCollisionStop();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsLiveCellCollider(other))
            HandleCollisionStop();
    }

    /// <summary>
    /// Place the cursor sprite at a starting world position and wait for a click
    /// to begin the level/simulation.
    /// </summary>
    public void PlaceAtStart(Vector2 worldPosition)
    {
        _hasCollided = false;
        _waitingForStart = true;
        followMouse = false;

        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

        Cursor.visible = true;

        var renderer = GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.enabled = true;
    }

    private void OnMouseDown()
    {
        if (!_waitingForStart) return;
        _waitingForStart = false;

        // Begin following the mouse and start the simulation.
        followMouse = true;
        _lastMouseScreen = Input.mousePosition;
        Cursor.visible = false;
        _collider.points = _playColliderPoints;

        if (simulation != null)
            simulation.StartSimulation();
    }
}

