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
    [Networked] public NetworkBool IsGameSceneLoaded { get; set; }
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
        
        // 캐릭터 선택 완료(모두 선택 OR 시간 만료) → 인게임 이동
        if (IsCharacterSelectActive &&
            (CharacterSelectTimer.Expired(Runner) || SelectedCharacters.Count == MaxPlayerCount) && !IsGameActive)
        {
            IsGameActive = true;
            RPC_GoToGame();
        }
        
        if (!IsCompleteSpawn && IsGameActive && IsGameSceneLoaded)
        {
            var system = FindAnyObjectByType<System_Test>();

            if (system == null)
            {
                Debug.Log("[MatchingManager] System_Test not found yet, waiting for scene load...");
                return;
            }

            if (Object.HasStateAuthority)
            {
                // 캐릭터를 선택하지 않은 플레이어에게 기본 캐릭터 할당
                foreach (var player in Runner.ActivePlayers)
                {
                    if (!SelectedCharacters.ContainsKey(player))
                    {
                        Debug.Log($"[MatchingManager] Player {player} has no character selected, assigning default");
                        SelectedCharacters.Add(player, CharacterDataEnum.Eva);
                    }
                }

                Debug.Log($"[MatchingManager] Spawning {SelectedCharacters.Count} characters");

                IsCompleteSpawn = true;

                foreach (var playerInfo in SelectedCharacters)
                {
                    var playerPrefab = system.SelectPrefab(playerInfo.Value);
                    NetworkObject networkPlayerObject =
                        Runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, playerInfo.Key);
                    Runner.SetPlayerObject(playerInfo.Key, networkPlayerObject);
                    Debug.Log($"[MatchingManager] Spawned character for player {playerInfo.Key}");
                }

                RPC_TurnOffSecondCamera();
                RPC_HideLoadingUI();
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
        Debug.Log("[MatchingManager] RPC_GoToGame called");

        // 로딩 UI 표시
        if (GameLoadingUI.Instance != null)
        {
            GameLoadingUI.Instance.Show("게임 로딩 중...");
        }

        // 메뉴 UI 전체 숨기기 (모든 클라이언트에서 실행)
        if (Controller != null)
        {
            Controller.gameObject.SetActive(false);
        }

        // 메뉴 씬의 메인 카메라 비활성화 (AudioListener 포함)
        if (spawner != null && spawner.MainCamera != null)
        {
            spawner.MainCamera.SetActive(false);
        }

        if (Object.HasStateAuthority) // 서버에서만 씬 로드 실행
        {
            Debug.Log("[MatchingManager] Loading Game_Cobalt scene...");
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

        // AudioListener 중복 방지: 씬에서 하나만 활성화
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 1; i < listeners.Length; i++)
        {
            listeners[i].enabled = false;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_HideLoadingUI()
    {
        Debug.Log("[MatchingManager] RPC_HideLoadingUI - Game ready!");

        // 로딩 UI 숨기기
        if (GameLoadingUI.Instance != null)
        {
            GameLoadingUI.Instance.Hide();
        }
    }
}
