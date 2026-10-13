using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI; // Wajib ditambahkan agar kita bisa mengakses NavMeshAgent
using UnityEngine.EventSystems;

public class PlayerAvatarController : MonoBehaviour
{
    private InputManager inputManager;
    private Camera mainCam;
    private MovableObjectMotor objectMotor;
    private LayerMask walkableMask;

    // --- VARIABEL WASD ---
    private NavMeshAgent agent;
    private bool isMovingWithWASD = false;
    public float wasdMoveSpeed = 4f;

    [SerializeField]
    [Tooltip("How many seconds will we give the Player to read the NPCs text when they bump into them")]
    float timeToPause = 0.5f;

    void Start()
    {
        mainCam = Camera.main;
        objectMotor = GetComponent<MovableObjectMotor>();
        walkableMask = 1 << 8;

        // Mengambil referensi NavMeshAgent dari karakter
        agent = GetComponent<NavMeshAgent>();

        inputManager = GameObject.Find("InputManagerObj").GetComponent<InputManager>();

        inputManager.ClickDownEvent += OnClickDown;
        inputManager.EscEvent += OnEscPressed;
        inputManager.WASDMoveEvent += OnWASDMoved;
    }

    public void OnEscPressed(object source, EventArgs e)
    {
        objectMotor.MoveObjectTo(transform.position);
    }

    public void OnClickDown(object source, EventArgs e)
    {
        // Jangan gerakkan Player kalau yang diklik adalah kontrol UI (Button/Slider), misalnya Slider di Canvas NPC
        if (IsPointerOverUIControl()) return;

        RaycastHit hit;
        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit, Mathf.Infinity, walkableMask))
        {
            isMovingWithWASD = false;
            objectMotor.MoveObjectTo(hit.point);
        }
    }

    // Mengecek apakah kursor/jari sedang berada di atas kontrol UI yang bisa diklik (Button, Slider, dll)
    private bool IsPointerOverUIControl()
    {
        if (EventSystem.current == null) return false;
        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = Input.touchCount > 0 ? (Vector2)Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);
        foreach (RaycastResult result in results)
        {
            if (result.gameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null) return true;
        }
        return false;
    }


    // ==========================================
    // LOGIKA GERAK WASD (Perbaikan NavMesh)
    // ==========================================
    public void OnWASDMoved(Vector3 movement)
    {
        // Pastikan agent tersedia agar tidak error
        if (agent == null) return;

        if (movement.magnitude >= 0.1f)
        {
            isMovingWithWASD = true;

            // 1. Batalkan rute dari klik kursor (agar tidak saling tarik-menarik)
            agent.ResetPath();

            // 2. Beri kecepatan langsung. Ini menggerakkan karakter sekaligus memicu animasinya!
            agent.velocity = movement.normalized * wasdMoveSpeed;

            // 3. Putar badan karakter ke arah gerak WASD
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement), 0.15f);
        }
        else if (isMovingWithWASD)
        {
            // Jika tombol WASD dilepas, rem karakternya
            isMovingWithWASD = false;
            agent.velocity = Vector3.zero;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.tag == "NPC" || other.gameObject.tag == "POI")
        {
            objectMotor.Stop();
            transform.LookAt(other.transform);
            StartCoroutine(PauseCharacter());
        }
    }

    IEnumerator PauseCharacter()
    {
        yield return new WaitForSeconds(timeToPause);
        objectMotor.Go();
    }
}