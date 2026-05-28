using UnityEngine;

public class AdamWeaponManager : MonoBehaviour
{
    private PlayerControls controls;

    [Header("Weapon References")]
    public GameObject gun3DModel;     // References the physical WrathGun object
    public MonoBehaviour meleeScript;  // References AdamMelee component
    public MonoBehaviour gunScript;    // References AdamFirearm component

    private void Awake()
    {
        controls = new PlayerControls();
    }

    private void OnEnable()
    {
        controls.Enable();
        controls.Player.SelectWeapon1.performed += ctx => EquipFists();
        controls.Player.SelectWeapon2.performed += ctx => EquipGun();
    }

    private void OnDisable()
    {
        controls.Disable();
        controls.Player.SelectWeapon1.performed -= ctx => EquipFists();
        controls.Player.SelectWeapon2.performed -= ctx => EquipGun();
    }

    private void Start()
    {
        // Start the game equipped with Fists by default
        EquipFists();
    }

    public void EquipFists()
    {
        Debug.Log("🥊 Fists Raised.");
        gun3DModel.SetActive(false); // Hide gun model
        meleeScript.enabled = true;  // Turn on punch script
        gunScript.enabled = false;   // Turn off shooting script
    }

    public void EquipGun()
    {
        Debug.Log("🔫 Wrath Gun Unholstered.");
        gun3DModel.SetActive(true);  // Show gun model
        meleeScript.enabled = false; // Turn off punch script
        gunScript.enabled = true;    // Turn on shooting script
    }
}