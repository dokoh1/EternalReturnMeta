using Fusion;
using Fusion.Menu;
using UnityEditor;
using UnityEngine;

public class CharacterSelectButton : MonoBehaviour
{
    public CharacterDataEnum _characterDataEnum;
    public MenuUIController Controller;
    
    public void OnClick()
    {
        var manager = MatchingManager.Instance;
        PlayerNetworkObject myPlayer = null;
        if (manager != null)
        { 
            PlayerRef myObj = MatchingManager.Instance.Runner.LocalPlayer;
            if (MatchingManager.Instance.Runner.TryGetPlayerObject(myObj, out var networkObject))
            {
                // myPlayer = networkObject.GetComponent<PlayerNetworkObject>();
                myPlayer = networkObject.GetComponent<PlayerNetworkObject>();
            }
            else
            {
                Debug.Log("TryGetPlayerObject failed");
            }
            
            if (myPlayer != null) 
                myPlayer.Rpc_RequestSelectCharacter(_characterDataEnum);
        }
        var ui = Controller.Get<FusionMenuUICharacterSelect>();
        ui.UpdateMyCharacterImage(_characterDataEnum);
    }
}