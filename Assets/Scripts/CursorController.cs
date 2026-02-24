using UnityEngine;

/// <summary>
/// Makes the attached sprite follow the mouse position in world space
/// after the level has started, and stop when it collides with something.
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

    private bool _hasCollided;
    private bool _waitingForStart;
    private Rigidbody2D _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
        _rb.useFullKinematicContacts = true;

        if (simulation == null)
            simulation = FindFirstObjectByType<GameOfLifeSimulation>();
    }

    private void Update()
    {
        if (!_hasCollided && followMouse)
        {
            MoveToMousePosition();
        }
    }

    private void MoveToMousePosition()
    {
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 mouseScreen = Input.mousePosition;

        // Distance from camera to this object so ScreenToWorldPoint works correctly in 2D.
        float depth = Mathf.Abs(cam.transform.position.z - transform.position.z);
        mouseScreen.z = depth;

        Vector3 worldPos = cam.ScreenToWorldPoint(mouseScreen);
        worldPos.z = transform.position.z;

        transform.position = worldPos;
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
        Cursor.visible = false;

        if (simulation != null)
            simulation.StartSimulation();
    }
}

