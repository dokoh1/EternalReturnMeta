namespace Fusion.Menu
{
    using System.Collections.Generic;
#if FUSION_ENABLE_TEXTMESHPRO
    using Text = TMPro.TMP_Text;
#else
    using Text = UnityEngine.UI.Text;
#endif
    using UnityEngine;

    // 메뉴 화면에 표시되는 서버 지역(Region)과 앱 버전(AppVersion)을 텍스트로 출력하는 UI 플러그인
    public class FusionMenuScreenPluginVersion : FusionMenuScreenPlugin
    {
        [SerializeField] protected Text _textField;

        public override void Show(FusionMenuUIScreen screen)
        {
            if (_textField != null)
            {
                if (screen.Connection != null && screen.Connection.IsConnected)
                    _textField.text = GetInformationalVersion(screen.Connection);
                else
                    _textField.text = GetInformationalVersion(screen.ConnectionArgs);
            }
        }

        public virtual string GetInformationalVersion(IFusionMenuConnectArgs connectionArgs)
        {
            if (connectionArgs == null)
                return string.Empty;
            
            return CreateInformationVersion(string.IsNullOrEmpty(connectionArgs.Region) ? connectionArgs.PreferredRegion : connectionArgs.Region, connectionArgs.AppVersion);
        }

        public virtual string GetInformationalVersion(IFusionMenuConnection connection)
        {
            if (connection == null)
                return string.Empty;
            
            return CreateInformationVersion(connection.Region, connection.AppVersion);
        }

        public virtual string CreateInformationVersion(string region, string appVersion)
        {
            var list = new List<string>();
            
            if (string.IsNullOrEmpty(region) == false)
                list.Add(region);
            
            else
                list.Add("Best Region");

            if (string.IsNullOrEmpty(appVersion) == false)
                list.Add(appVersion);

            if (list.Count == 0)
                return null;
            
            return string.Join(" | ", list);
        }
    }
}