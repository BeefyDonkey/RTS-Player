using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using Mirror;

public class RTSPlayer : NetworkBehaviour
{
    private List<Unit> myUnits = new List<Unit>();
    private List<Building> myBuildings = new List<Building>(); //List of already built buildings

    [SerializeField] private Building[] buildings; //Array of buildings available for building
    [SerializeField] private LayerMask buildingBlockLayer = new LayerMask();
    [SerializeField] private float buildingRangeLimit = 10f;

    [SerializeField] public Material[] unitTeamColors;
    [SerializeField] public Material[] buildingTeamColors;

    [SyncVar(hook = nameof(ClientHandleGoldUpdated))]
    private int gold = 500;

    private int teamColorIndex = -1;

    public event Action<int> ClientOnGoldUpdated;

    //Getters

    public List<Unit> GetMyUnits() => myUnits;

    public List<Building> GetMyBuildings() => myBuildings;

    public int GetGold() => gold;

    public int GetTeamColorIndex() => teamColorIndex;

    //Setters

    [Server]
    public void SetTeamColorIndex(int newTeamColorIndex)
    {
        teamColorIndex = newTeamColorIndex;
    }

    [Server]
    public void SetGold(int newGold)
    {
        gold = newGold;
    }

    public bool CanPlaceBuilding(BoxCollider buildingCollider, Vector3 spawnPosition)
    {
        //Are we overlapping with other buildings

        if (Physics.CheckBox
            (spawnPosition + buildingCollider.center,
            buildingCollider.size / 2,
            Quaternion.identity,
            buildingBlockLayer
            ))
        {
            return false;
        }

        //Are we in range

        foreach (Building building in myBuildings)
        {
            if ((spawnPosition - building.transform.position).sqrMagnitude
                <= buildingRangeLimit * buildingRangeLimit)
            {
                return true;
            }
        }

        return false;
    }

    #region Server    

    public override void OnStartServer()
    {
        Unit.ServerOnUnitSpawned += ServerHandleUnitSpawned;
        Unit.ServerOnUnitDespawned += ServerHandleUnitDespawned;

        Building.ServerOnBuildingSpawned += ServerHandleBuildingSpawned;
        Building.ServerOnBuildingDespawned += ServerHandleBuildingDespawned;
    }

    public override void OnStopServer()
    {
        Unit.ServerOnUnitSpawned -= ServerHandleUnitSpawned;
        Unit.ServerOnUnitDespawned -= ServerHandleUnitDespawned;

        Building.ServerOnBuildingSpawned -= ServerHandleBuildingSpawned;
        Building.ServerOnBuildingDespawned -= ServerHandleBuildingDespawned;
    }

    //Building Spawn Command

    [Command]
    public void CmdTryPlaceBuilding(int buildingId, Vector3 spawnPosition, Quaternion buildingRotation)
    {
        Building buildingToSpawn = null;

        foreach (Building building in buildings)
        {
            if (building.GetId() == buildingId)
            {
                buildingToSpawn = building;
                break;
            }
        }

        if (buildingToSpawn == null) return;

        if (gold < buildingToSpawn.GetPrice()) return; //Do we have enough gold

        BoxCollider buildingCollider = buildingToSpawn.GetComponent<BoxCollider>();


        if (!CanPlaceBuilding(buildingCollider, spawnPosition)) return;


        var newBuilding = Instantiate
            (buildingToSpawn.gameObject, spawnPosition, buildingRotation);

        NetworkServer.Spawn(newBuilding, connectionToClient);

        SetGold(gold - buildingToSpawn.GetPrice()); //Reduce player gold
    }

    //Unit Handlers

    private void ServerHandleUnitSpawned(Unit unit)
    {
        if (unit.connectionToClient.connectionId != connectionToClient.connectionId) return;

        myUnits.Add(unit);
    }

    private void ServerHandleUnitDespawned(Unit unit)
    {
        if (unit.connectionToClient.connectionId != connectionToClient.connectionId) return;

        myUnits.Remove(unit);
    }

    //Building Handlers

    private void ServerHandleBuildingSpawned(Building building)
    {
        if (building.connectionToClient.connectionId != connectionToClient.connectionId) return;

        myBuildings.Add(building);
    }

    private void ServerHandleBuildingDespawned(Building building)
    {
        if (building.connectionToClient.connectionId != connectionToClient.connectionId) return;

        myBuildings.Remove(building);
    }


    #endregion

    #region Client

    public override void OnStartAuthority()
    {
        if (NetworkServer.active) return; //Check if we are hosting the server

        Unit.AuthorityOnUnitSpawned += AuthorityHandleUnitSpawned;
        Unit.AuthorityOnUnitDespawned += AuthorityHandleUnitDespawned;

        Building.AuthoriyOnBuildingSpawned += AuthorityHandleBuildingSpawned;
        Building.AuthorityOnBuildingDespawned += AuthorityHandleBuildingDespawned;
    }

    public override void OnStopClient()
    {
        if (!isClientOnly || !hasAuthority) return;

        Unit.AuthorityOnUnitSpawned -= AuthorityHandleUnitSpawned;
        Unit.AuthorityOnUnitDespawned -= AuthorityHandleUnitDespawned;

        Building.AuthoriyOnBuildingSpawned -= AuthorityHandleBuildingSpawned;
        Building.AuthorityOnBuildingDespawned -= AuthorityHandleBuildingDespawned;
    }

    //Unit Handlers

    private void AuthorityHandleUnitSpawned(Unit unit)
    {
        myUnits.Add(unit);
    }

    private void AuthorityHandleUnitDespawned(Unit unit)
    {
        myUnits.Remove(unit);
    }

    //Building Handlers

    private void AuthorityHandleBuildingSpawned(Building building)
    {
        myBuildings.Add(building);
    }

    private void AuthorityHandleBuildingDespawned(Building building)
    {
        myBuildings.Remove(building);
    }

    //Resources Handlers

    private void ClientHandleGoldUpdated(int oldAmount, int newAmount)
    {
        ClientOnGoldUpdated?.Invoke(newAmount);
    }

    #endregion
}
