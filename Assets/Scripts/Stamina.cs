using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;
public class StaminaSystem : MonoBehaviour
{
    public float maxStamina = 100f;
    public float drainRate = 5f;


    [SerializeField] private PlayerHUD hud;

    private float currentStamina;
    private Vector2 startPosition;
    private Vector2 respawnPosition;
    private bool isInSafeZone = false;

    private Rigidbody2D rb;
    private PlayerMovement playerMovement;
    private ShadowAnchor anchor;

    public bool IsInSafeZone => isInSafeZone;
    public bool IsDead { get; private set; }

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float deathAnimDuration = 1f;

    void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();
        anchor = GetComponent<ShadowAnchor>();
    }
    public void Die()
    {
        if (IsDead) return;
        StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        IsDead = true;

        if (anchor != null)
            anchor.ForceDestroyAnchor();

        if (playerMovement != null)
        {
            playerMovement.ResetMovementState();
            playerMovement.enabled = false; // Bloquea el movimiento durante la animación
        }

        if (animator != null)
            animator.SetTrigger("Die");

        yield return new WaitForSeconds(deathAnimDuration);

        if (rb != null)
        {
            rb.position = respawnPosition;
            rb.linearVelocity = Vector2.zero;
        }

        transform.position = respawnPosition;
        currentStamina = maxStamina;

        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null)
            health.ResetAfterRespawn();

        if (playerMovement != null)
            playerMovement.enabled = true;

        IsDead = false;
    }

    void Start()
    {
        currentStamina = maxStamina;
        startPosition = transform.position;
        respawnPosition = startPosition;



    }
    void Update()
    {
            if (isInSafeZone)
            {
                RechargeStamina();
            }
            else
            {
                // Drain stamina continuously
                currentStamina -= drainRate * Time.deltaTime;
            }


            if (currentStamina <= 0f)
            {
                Die();
            }
    }
    

    
    private void LateUpdate()
    {
        // Update the radial stamina bar.
        if (hud != null)
            hud.SetStamina(currentStamina, maxStamina);

        
    }

    public bool TryRestoreStamina(float amount)
    {
        if (amount <= 0f || currentStamina >= maxStamina)
            return false;

        currentStamina = Mathf.Min(currentStamina + amount, maxStamina);


        return true;
    }

    public void SetSafeZone(bool state)
    {
        isInSafeZone = state;
        if (state)
        {
            RechargeStamina();
        }
    }


    public void RechargeStamina()
    {
        currentStamina = maxStamina;

    }

    public void SetRespawnPoint(Vector2 newPosition)
    {
        respawnPosition = newPosition;
    }
}