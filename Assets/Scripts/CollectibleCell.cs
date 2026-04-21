using UnityEngine;

/// <summary>
/// Collectible \"cell\" that speeds up the Game of Life simulation when picked up.
/// Place these in the level (e.g. 5 of them); each pickup makes the
/// simulation run faster by a configurable multiplier.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CollectibleCell : MonoBehaviour
{
    [Header("Simulation Reference")]
    [Tooltip("Simulation whose timestep will be sped up when this is collected.")]
    [SerializeField] private GameOfLifeSimulation simulation;

    [Header("Speed Settings")]
    [Tooltip("Each collected cell makes the simulation this many times faster. 1.5 = 1.5x faster.")]
    [SerializeField] private float speedMultiplierPerPickup = 1.5f;

    [Tooltip("Destroy the collectible when picked up. If false, it will just be deactivated.")]
    [SerializeField] private bool destroyOnCollect = true;

    [Header("Player Filtering")]
    [Tooltip("Only objects with this tag can collect the cell. Leave empty to allow any collector.")]
    [SerializeField] private string playerTag = "";

    private bool _collected;

    private void Awake()
    {
        if (simulation == null)
            simulation = FindFirstObjectByType<GameOfLifeSimulation>();
    }

    private void TryCollect(GameObject collector)
    {
        if (_collected) return;
        // If a playerTag is set, require it unless this is the cursor controller.
        if (!string.IsNullOrEmpty(playerTag) &&
            !collector.CompareTag(playerTag) &&
            collector.GetComponent<CursorController>() == null)
        {
            return;
        }

        _collected = true;
        AudioManager.Instance.PlayCollectSound();

        if (simulation != null)
        {
            if (speedMultiplierPerPickup > 0f)
                simulation.MultiplyStepSpeed(speedMultiplierPerPickup);

            // Notify the simulation that one collectible was taken so it can
            // trigger the level transition when all are collected.
            simulation.OnCollectibleCollected();
        }

        if (destroyOnCollect)
            Destroy(gameObject);
        else
            gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryCollect(collision.collider.gameObject);
    }
}

