using System.Linq;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.SimpleKCC;
using Fusion.Menu;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MatchingManager : NetworkBehaviour
{
    public static MatchingManager Instance { get; private set; }
    [Networked] public NetworkBool IsMatchingComplete { get; set; }
    [Networked] public TickTimer LoadingTimer { get; set; }
    [Networked] public TickTimer CharacterSelectTimer { get; set; }
    [Networked] public NetworkBool IsCharacterSelectActive { get; set; }
    [Networked] public NetworkBool IsGameActive { get; set; }
    [Networked] public NetworkBool IsCompleteSpawn { get; set; }
    [Networked, Capacity(2)]
    public NetworkDictionary<PlayerRef, CharacterDataEnum> SelectedCharacters => default;
    
    private int MaxPlayerCount { get; set; } = 2;
    public const float LoadingDuration = 5f;
    public const float CharacterSelectDuration = 20f;
    public MenuUIController Controller { get; set; }
    
    private MatchingManagerSpawner spawner;
    
    public override void Spawned()
    {
        Instance = this;
        if (Controller == null)
            Controller = FindAnyObjectByType<MenuUIController>();
        spawner = FindAnyObjectByType<MatchingManagerSpawner>();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void Rpc_SelectCharacter(CharacterDataEnum characterId, PlayerRef playerRef)
    {
        if (!SelectedCharacters.ContainsKey(playerRef))
        {
            SelectedCharacters.Add(playerRef, characterId);
        }
        else
        {
            SelectedCharacters.Set(playerRef, characterId);
        }
    }
    
    // MaxPlayer를 가져옴
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_UpdatePlayerCount(int ready, int max)
    {
        if (!HasStateAuthority)
            return;
        Controller.Get<MatchingModal>().UpdatePlayerCount(ready, max);
    }

    public override void FixedUpdateNetwork()
    {
        
        if (Controller == null)
        {
            Debug.Log("Controller is null");
        }
        if (!Object.HasStateAuthority) return;
        
        
        int currentPlayers = Runner.ActivePlayers.Count();
        RPC_UpdatePlayerCount(currentPlayers, MaxPlayerCount);
        if (!IsMatchingComplete && Runner.ActivePlayers.Count() == MaxPlayerCount)
        {
            IsMatchingComplete = true;
            LoadingTimer = TickTimer.CreateFromSeconds(Runner, LoadingDuration);
            RPC_ShowLoading();
        }
        if (IsMatchingComplete && LoadingTimer.Expired(Runner) && !IsCharacterSelectActive)
        {
            CharacterSelectTimer = TickTimer.CreateFromSeconds(Runner, CharacterSelectDuration);
            IsCharacterSelectActive = true;
            RPC_GoToCharacterSelect();
        }
        
        // 캐릭터 선택 완료(모두 선택 or 시간 만료) → 인게임 이동
        if (IsCharacterSelectActive &&
            (CharacterSelectTimer.Expired(Runner) && SelectedCharacters.Count == MaxPlayerCount) && !IsGameActive)
        {
            IsGameActive = true;
            RPC_GoToGame();
        }
        
        if (!IsCompleteSpawn)
        {
            var system = FindAnyObjectByType<System_Test>();

            if (system == null)
            {
                return;
            }
            
            if (Object.HasStateAuthority)
            {
                IsCompleteSpawn = true;

                foreach (var playerInfo in SelectedCharacters)
                {
                    var playerPrefab = system.SelectPrefab(playerInfo.Value);
                    NetworkObject networkPlayerObject =
                        Runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, playerInfo.Key);
                    Runner.SetPlayerObject(playerInfo.Key, networkPlayerObject);
                } 
                
                RPC_TurnOffSecondCamera();
                spawner.IsCompleteSpawn = true;
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowLoading()
    {
        Controller.Show<FusionMenuUILoading>();
        
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_GoToCharacterSelect()
    {
        Controller.Show<FusionMenuUICharacterSelect>();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_GoToGame()
    {
        if (Object.HasStateAuthority) // 서버에서만 씬 로드 실행
        {
            // GameScene을 Additive 모드로 로드
            Runner.LoadScene(
                SceneRef.FromIndex(1), 
                LoadSceneMode.Additive
            );
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_TurnOffSecondCamera()
    {
        spawner.MainCamera.SetActive(false);
    }
}
