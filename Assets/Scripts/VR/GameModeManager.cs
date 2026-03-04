using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public Transform robot;
    public Transform player;

    public MapGameHelper mapHelper;
    public GameObject targetPrefab;
    public GameObject energyPrefab;

    private GameObject targetA;
    private GameObject targetB;
    private GameObject energyObject;

    private int level = 0;
    private bool gameActive = false;

    private float syncTimer = 0f;

    private bool playerHasEnergy = false;
    private bool robotHasEnergy = false;

    public void StartGameMode()
    {
        if (mapHelper.occupancy == null) return;

        level = 1;
        gameActive = true;
        StartLevel();
    }

    void StartLevel()
    {
        ClearLevel();

        switch (level)
        {
            case 1:
                targetA = SpawnTarget(robot.position);
                break;

            case 2:
                targetA = SpawnTarget(player.position);
                break;

            case 3:
                targetA = SpawnTarget(robot.position);
                targetB = SpawnTarget(player.position);
                break;

            case 4:
                energyObject = SpawnEnergy(player.position);
                targetA = SpawnTarget(robot.position);
                break;
        }
    }

    void Update()
    {
        if (!gameActive) return;


        if (Input.GetKeyDown(KeyCode.V))
        {
            Debug.Log("Nivel saltado manualmente (tecla V)");
            NextLevel();
            return;
        }


        switch (level)
        {
            case 1:
                if (Reached(robot, targetA))
                    NextLevel();
                break;

            case 2:
                if (Reached(player, targetA))
                    NextLevel();
                break;

            case 3:
                HandleSyncLevel();
                break;

            case 4:
                HandleEnergyLevel();
                break;
        }
    }

    void HandleSyncLevel()
    {
        bool robotIn = Reached(robot, targetA);
        bool playerIn = Reached(player, targetB);

        if (robotIn && playerIn)
        {
            syncTimer += Time.deltaTime;

            if (syncTimer >= 2f)
                NextLevel();
        }
        else
        {
            syncTimer = 0f;
        }
    }

    void HandleEnergyLevel()
    {
        if (!playerHasEnergy && Reached(player, energyObject))
        {
            playerHasEnergy = true;
            energyObject.SetActive(false);
        }

        if (playerHasEnergy &&
            Vector3.Distance(player.position, robot.position) < 0.7f)
        {
            robotHasEnergy = true;
            playerHasEnergy = false;
        }

        if (robotHasEnergy && Reached(robot, targetA))
        {
            NextLevel();
        }
    }

    public bool RobotCanMove()
    {
        // Solo bloquear durante nivel 4 activo
        if (gameActive && level == 4 && !robotHasEnergy)
            return false;

        return true;
    }


    GameObject SpawnTarget(Vector3 farFrom)
    {
        Vector3 pos = mapHelper.GetRandomFreeWorldPosition(2f);
        return Instantiate(targetPrefab, pos, Quaternion.identity);
    }

    GameObject SpawnEnergy(Vector3 farFrom)
    {
        Vector3 pos = mapHelper.GetRandomFreeWorldPosition(2f);
        return Instantiate(energyPrefab, pos, Quaternion.identity);
    }

    bool Reached(Transform t, GameObject obj)
    {
        return Vector3.Distance(t.position, obj.transform.position) < 0.5f;
    }

    void NextLevel()
    {
        level++;

        if (level > 4)
        {
            Debug.Log("Juego completado");
            gameActive = false;

            robotHasEnergy = false;
            playerHasEnergy = false;

            ClearLevel();
            level = 0;
        }
        else
        {
            StartLevel();
        }
    }

    void ClearLevel()
    {
        if (targetA) Destroy(targetA);
        if (targetB) Destroy(targetB);
        if (energyObject) Destroy(energyObject);

        syncTimer = 0f;
        playerHasEnergy = false;
        robotHasEnergy = false;
    }
}
