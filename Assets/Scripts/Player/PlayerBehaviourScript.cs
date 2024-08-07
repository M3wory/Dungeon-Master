using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cinemachine;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerBehaviourScript : MonoBehaviour
{
    [Header("Player Settings")]
    public float maxHP;
    public float currentHP;
    public float damageCooldown;
    [Space]
    public float maxMana;
    public float currentMana;
    [Space]
    public float damage;
    [Space]
    public float speed;
    [Space]
    public float ultaCost;
    [Space]
    public int coins;

    [Header("Components")]
    public GunBehaviour gun;
    public Image healthBar;
    public TMP_Text healthText;
    public Image manaBar;
    public TMP_Text manaText;
    public Image ammoIndicator;
    public TMP_Text ammoText;
    public TMP_Text coinsText;
    public GameObject UltaEffect;
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private CinemachineImpulseSource shake;
    private AudioSource audioSource;
    public DungeonGenerator dungeonGenerator;

    [Header("Audio Clips")]
    public AudioClip takeDamageClip;
    public AudioClip healClip;
    public AudioClip ultaClip;
    public AudioClip pickupClip;

    [Header("Private variables")]
    private Vector2 movement;
    private bool isFlashing;
    private bool canTakeDamage = true;
    private bool startedReloadAnimation;
    private bool hasKey;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        shake = GetComponent<CinemachineImpulseSource>();
        audioSource = GetComponent<AudioSource>();

        currentHP = maxHP;
        currentMana = maxMana;

        UpdateHealthVisual();
        UpdateManaVisual();
    }

    private void FixedUpdate()
    {
        rb.velocity = movement.normalized * speed;

        RotatePlayerTowardsMouse();
        RotateWeapon();
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        anim.SetBool("Run", movement.magnitude != 0);

        if (Input.GetKeyDown(KeyCode.Space) && currentMana >= ultaCost)
        {
            SpendMana(ultaCost);
            Ulta();
        }

        if (Input.GetMouseButton(0))
        {
            if (gun.Shoot(true, currentMana))
            {
                SpendMana(gun.manaCost);
                shake.m_DefaultVelocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
                shake.GenerateImpulse();
            }
        }

        if (Input.GetKeyDown(KeyCode.R) && !gun.isReloading)
        {
            StartCoroutine(gun.Reload());
        }

        UpdateAmmoIndicator();

        InterractWithNearby();

        coinsText.text = coins.ToString();
    }

    private void InterractWithNearby(){
        Collider2D[] nearbyWeapons = Physics2D.OverlapCircleAll(transform.position, 1f);

        foreach (Collider2D collider in nearbyWeapons)
        {
            if (collider.CompareTag("Weapon") && Input.GetKeyDown(KeyCode.E) && !gun.isReloading){
                PlaySound(pickupClip);
                gun.Replace(collider.GetComponent<GunBehaviour>());
                break;
            }

            if (collider.CompareTag("HealthPotion") && Input.GetKeyDown(KeyCode.E)){
                Heal(maxHP);
                Destroy(collider.gameObject);
                break;
            }

            if (collider.CompareTag("ManaPotion") && Input.GetKeyDown(KeyCode.E)){
                RestoreMana(maxMana);
                Destroy(collider.gameObject);
                break;
            }

            if (collider.CompareTag("Chest") && Input.GetKeyDown(KeyCode.E) && coins >= collider.GetComponent<ChestScript>().cost){
                collider.GetComponent<ChestScript>().DestroyObject();
                coins -= collider.GetComponent<ChestScript>().cost;
                break;
            }

            if (collider.CompareTag("Coin")){
                coins++;
                PlaySound(pickupClip);
                Destroy(collider.gameObject);
                break;
            }
        }
    }

    private void UpdateAmmoIndicator()
    {
        int currentAmmo = gun.GetCurrentAmmo();
        int magazineSize = gun.GetMagazineSize();
        ammoIndicator.fillAmount = (float)currentAmmo / magazineSize;
        if (gun.isReloading)
        {
            if(!startedReloadAnimation){
                ammoText.text = "Reloading...";
                StartCoroutine(AnimateReload(gun.reloadTime));
            }
        }
        else
        {
            startedReloadAnimation = false;
            ammoText.text = currentAmmo.ToString() + "/" + magazineSize.ToString();
        }
    }

    private IEnumerator AnimateReload(float reloadTime)
    {
        startedReloadAnimation = true;
        float elapsedTime = 0f;
        while (elapsedTime < reloadTime)
        {
            ammoIndicator.fillAmount = elapsedTime / reloadTime;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        ammoIndicator.fillAmount = 1f;
    }

    private void RotatePlayerTowardsMouse()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePosition.z = 0;

        Vector3 direction = mousePosition - transform.position;
        direction.Normalize();

        if (direction.x > 0)
        {
            spriteRenderer.flipX = false;
        }
        else if (direction.x < 0)
        {
            spriteRenderer.flipX = true;
        }
    }

    private void RotateWeapon()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        Vector3 direction = mousePosition - gun.transform.position;
        direction.z = 0f;

        direction.Normalize();

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        gun.transform.rotation = Quaternion.Euler(new Vector3(0f, 0f, angle));
    }

    public void TakeDamage(float amount)
    {
        if (!canTakeDamage) return;

        currentHP -= amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        shake.m_DefaultVelocity = new Vector3(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0);
        shake.GenerateImpulse();

        PlaySound(takeDamageClip);

        if (!isFlashing)
        {
            StartCoroutine(FlashRed());
        }

        if (currentHP == 0)
        {
            Die();
        }

        UpdateHealthVisual();

        StartCoroutine(DamageCooldown());
    }

    private IEnumerator DamageCooldown()
    {
        canTakeDamage = false;
        yield return new WaitForSeconds(damageCooldown);
        canTakeDamage = true;
    }

    public void Die()
    {
        SceneManager.LoadScene(4);
    }

    public void Heal(float amount)
    {
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        PlaySound(healClip);

        UpdateHealthVisual();
    }

    public void SpendMana(float amount)
    {
        currentMana -= amount;
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);

        UpdateManaVisual();
    }

    public void RestoreMana(float amount)
    {
        currentMana += amount;
        currentMana = Mathf.Clamp(currentMana, 0, maxMana);

        PlaySound(healClip);

        UpdateManaVisual();
    }

    private void UpdateHealthVisual()
    {
        healthBar.fillAmount = currentHP / maxHP;
        healthText.text = currentHP.ToString() + "/" + maxHP.ToString();
    }

    private void UpdateManaVisual()
    {
        manaBar.fillAmount = currentMana / maxMana;
        manaText.text = currentMana.ToString() + "/" + maxMana.ToString();
    }

    private IEnumerator FlashRed()
    {
        isFlashing = true;
        spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        spriteRenderer.color = Color.white;
        isFlashing = false;
    }

    public void Ulta()
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position, 3f);

        foreach (Collider2D collider in colliders)
        {
            Rigidbody2D rb2d = collider.GetComponent<Rigidbody2D>();
            if (rb2d != null)
            {
                Vector2 direction = rb2d.transform.position - transform.position;
                direction.Normalize();

                rb2d.AddForce(direction * 5000f, ForceMode2D.Force);
            }
        }
        var effectObject = Instantiate(UltaEffect, transform.position, Quaternion.identity);
        Destroy(effectObject, 1f);
        PlaySound(ultaClip);
    }

    public void OnTriggerEnter2D(Collider2D col){
        if(col.CompareTag("Key")){
            hasKey = true;
            Destroy(col.gameObject);
            PlaySound(pickupClip);
        }else if(col.CompareTag("Stairs") && hasKey){
            hasKey = false;
            dungeonGenerator.NextFloor();
        }else if(col.CompareTag("Princess")){
            SceneManager.LoadScene(3);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }
}
