using System.Collections.Generic;
using UnityEngine;

public class TileManager : MonoBehaviour
{
    public GameObject[] tilePrefabs;
    public Transform target;
    public int numberOfTiles = 5;

    private List<GameObject> activeTiles = new List<GameObject>();
    private Transform lastSpawnPoint; // keeps track of where to spawn next

    private void OnEnable()
    {
        // Subscribe to GameManager event
        GameManager.OnPlayerChanged += HandlePlayerChanged;
    }

    private void OnDisable()
    {
        // Unsubscribe to avoid memory leaks
        GameManager.OnPlayerChanged -= HandlePlayerChanged;
    }
    private void HandlePlayerChanged(Transform newPlayer)
    {
        // Update the camera’s target when player changes
        target = newPlayer;
        Debug.Log("CameraFollow: Target updated to new player " + newPlayer.name);
    }
    private void Awake()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            target = playerObj.transform;
        }
        else
        {
            Debug.LogWarning("No GameObject found with tag 'Player'!");
        }
    }
    void Start()
    {
        // spawn initial tiles
        for (int i = 0; i < numberOfTiles; i++)
        {
            if (i == 0)
                SpawnTile(0); // first tile, at origin
            else
                SpawnTile();
        }
    }

    void Update()
    {
        // spawn new tile when player moves forward enough
        if (target.position.z - 35 > activeTiles[0].transform.position.z + 80f)
        {
            SpawnTile();
            DeleteTile();
        }
    }

    void SpawnTile(int prefabIndex = -1)
    {
        if (prefabIndex == -1)
            prefabIndex = Random.Range(0, tilePrefabs.Length);

        GameObject newTile;

        if (lastSpawnPoint == null)
            newTile = Instantiate(tilePrefabs[prefabIndex], Vector3.zero, Quaternion.identity);
        else
            newTile = Instantiate(tilePrefabs[prefabIndex], lastSpawnPoint.position, Quaternion.identity);

        // update spawn point
        Transform spawnPoint = newTile.transform.Find("SpawnPoint");
        if (spawnPoint != null)
            lastSpawnPoint = spawnPoint;

        // spawn coins/obstacles
        Tile tileScript = newTile.GetComponent<Tile>();
        if (tileScript != null)
            tileScript.SpawnItems();

        activeTiles.Add(newTile);
    }





    void DeleteTile()
    {
        Destroy(activeTiles[0]);
        activeTiles.RemoveAt(0);
    }
}
