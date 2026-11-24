// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using UnityEngine;

namespace CivilianSystem.Demo
{
    /// <summary>
    /// Minimal demo player: WASD + mouse look, left-click raycast damages civilians.
    /// Implements IDamageable so aggressive civilians can hurt it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DemoPlayer : MonoBehaviour, IDamageable
    {
        public float moveSpeed = 5f;
        public float lookSensitivity = 2f;
        public float attackDamage = 25f;
        public float attackRange = 30f;
        public float maxHealth = 100f;

        public Camera cam;

        CharacterController cc;
        float pitch;
        float health;
        Vector3 spawnPos;
        string statusMessage = "";
        float messageUntil;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            health = maxHealth;
            spawnPos = transform.position;
            if (cam == null) cam = GetComponentInChildren<Camera>();
        }

        void OnEnable() => WantedSystem.OnArrested += HandleArrested;
        void OnDisable() => WantedSystem.OnArrested -= HandleArrested;

        void HandleArrested()
        {
            // arrested = teleport back + heal, NOT death
            Teleport(spawnPos);
            health = maxHealth;
            ShowMessage("ARRESTED — released at spawn", 4f);
        }

        void Respawn()
        {
            Teleport(spawnPos);
            health = maxHealth;
            WantedSystem.Clear();
            ShowMessage("YOU DIED — respawned", 4f);
        }

        void Teleport(Vector3 pos)
        {
            cc.enabled = false;
            transform.position = pos;
            cc.enabled = true;
        }

        void ShowMessage(string msg, float dur)
        {
            statusMessage = msg;
            messageUntil = Time.time + dur;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        void Update()
        {
            // look
            float mx = Input.GetAxis("Mouse X") * lookSensitivity;
            float my = Input.GetAxis("Mouse Y") * lookSensitivity;
            transform.Rotate(0, mx, 0);
            pitch = Mathf.Clamp(pitch - my, -80f, 80f);
            if (cam != null) cam.transform.localEulerAngles = new Vector3(pitch, 0, 0);

            // move
            Vector3 input = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            Vector3 move = transform.TransformDirection(input) * moveSpeed;
            move.y = -9.81f;
            cc.Move(move * Time.deltaTime);

            // attack
            if (Input.GetMouseButtonDown(0) && cam != null)
            {
                Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                if (Physics.Raycast(ray, out var hit, attackRange))
                {
                    var dmg = hit.collider.GetComponentInParent<IDamageable>();
                    if (dmg != null)
                    {
                        dmg.TakeDamage(attackDamage, gameObject);
                        Debug.Log($"[DemoPlayer] Hit {hit.collider.transform.root.name} for {attackDamage}");
                    }
                }
            }

            // unlock cursor
            if (Input.GetKeyDown(KeyCode.Escape))
                Cursor.lockState = CursorLockMode.None;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            health = Mathf.Max(0, health - amount);
            if (health <= 0) Respawn();
        }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 500, 22), $"HP: {health:0}   |   WASD move, Mouse look, LMB attack, E talk, ESC unlock");
            if (WantedSystem.IsWanted)
                GUI.Label(new Rect(10, 34, 300, 22), "★ WANTED — police searching for you");
            if (Time.time < messageUntil)
                GUI.Label(new Rect(Screen.width / 2 - 150, 80, 300, 30), statusMessage);
            GUI.Label(new Rect(Screen.width / 2 - 4, Screen.height / 2 - 8, 20, 20), "+");
        }
    }
}
