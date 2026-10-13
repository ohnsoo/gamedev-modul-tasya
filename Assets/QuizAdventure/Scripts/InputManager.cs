using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class InputManager : MonoBehaviour
{
    // *********  Event and Handler for when the player clicks a mouse button *******
    public delegate void ClickDownHandler(object source, EventArgs args);
    public event ClickDownHandler ClickDownEvent;

    // ********* Event and Handler for when the player presses ESC **********
    public delegate void EscHandler(object source, EventArgs args);
    public event EscHandler EscEvent;

    // --- TAMBAHAN BONUS CHALLENGE: Event untuk mengirim arah WASD ---
    public event Action<Vector3> WASDMoveEvent;

    [HideInInspector]
    static public bool bCanPlayerMove = true;  // Can the player move? T or F

    private void Awake()
    {
        InputManager.bCanPlayerMove = true;  // Make sure the player can move
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)  //Check if the player is allowed to move and if the player has clicked the Left Mouse Button or pressed the mobile screen
        {
            if (bCanPlayerMove)
            {
                OnClickDown();  // Call Click Event
            }
        }
        if (Input.GetKeyDown(KeyCode.Escape)) //Check if the player is allowed to move and if the player has clicked the ESC key
        {
            if (bCanPlayerMove)
            {
                OnEscPressed();  // Call ESC Pressed Event
            }
        }

        // --- TAMBAHAN BONUS CHALLENGE: Deteksi Input WASD ---
        if (bCanPlayerMove)
        {
            float moveX = Input.GetAxisRaw("Horizontal"); // A/D atau panah Kiri/Kanan
            float moveZ = Input.GetAxisRaw("Vertical");   // W/S atau panah Atas/Bawah

            // Membuat vektor arah berdasarkan input
            Vector3 movement = new Vector3(moveX, 0f, moveZ).normalized;

            // Jika ada tombol yang ditekan
            if (movement.magnitude >= 0.1f)
            {
                WASDMoveEvent?.Invoke(movement); // Kirim arah ke PlayerAvatarController
            }
            else
            {
                WASDMoveEvent?.Invoke(Vector3.zero); // Beritahu bahwa tombol dilepas
            }
        }
    }

    // Used if the player clicks on the Home icon in the GUI
    public void EscapeGUIPressed()
    {
        OnEscPressed();  // Call ESC Pressed Event
    }

    // *********** Event ***********
    protected virtual void OnEscPressed()
    {
        if (EscEvent != null)
        {
            EscEvent(this, EventArgs.Empty);
        }
    }

    // *********** Event ***********
    protected virtual void OnClickDown()
    {
        if (ClickDownEvent != null)
        {
            ClickDownEvent(this, EventArgs.Empty);
        }
    }
}