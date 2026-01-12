namespace Fusion.Menu {
  using System;
  using System.Security.Cryptography;
  using System.Text;
  using UnityEngine;

// Fusion 메뉴 시스템에서 파티 코드(초대 코드, 룸 코드 등)을 생성하고 검증하는 유틸리티 클래스
  [CreateAssetMenu(menuName = "Fusion/Menu/Party Code Generator")]
  public class FusionMenuPartyCodeGenerator : FusionScriptableObject {
    [InlineHelp] public string ValidCharacters = "ABCDEFGHIJKLMNPQRSTUVWXYZ123456789";
    [InlineHelp, Range(1, 32)] public int Length = 8;
    [InlineHelp, Range(1, 32)] public int EncodedRegionPosition = 4;


    //기본 길이 만큼 파티 코드 생성
    public virtual string Create() {
      return Create(Length);
    }

    // 주어진 길이와 현재의 ValidCharacters를 사용해서 코드 생성
    public virtual string Create(int length) {
      return Create(length, ValidCharacters);
    }

    // 주어진 길이와 문자 목록을 기반으로 암호학적으로 안전한 무작위 코드 생성
    public static string Create(int length, string validCharacters) {
      length = Math.Max(1, Math.Min(length, 128));

      // m = 238 = highest multiple of 34 in 255
      var m = Mathf.FloorToInt((255.0f / validCharacters.Length)) * validCharacters.Length;
      if (m <= 0) {
        Debug.LogError($"Number of valid character ({validCharacters.Length}) has to be less than 255.");
        return null;
      }

      var res = new StringBuilder();
      using (RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider()) {
        while (res.Length != length) {
          var bytes = new byte[8];
          provider.GetBytes(bytes);
          foreach (var b in bytes) {
            if (b >= m || res.Length == length) continue;
            var character = validCharacters[b % validCharacters.Length];
            res.Append(character);
          }
        }
      }
      return res.ToString();
    }

    // 입력된 코드가 유효한지 검사
    public virtual bool IsValid(string code) {
      return IsValid(code, Length);
    }

    // 코드 검증 유틸리티
    public virtual bool IsValid(string code, int length) {
      if (string.IsNullOrEmpty(code)) {
        return false;
      }

      if (code.Length != Length) {
        return false;
      }

      for (int i = 0; i < code.Length; i++) {
        if (ValidCharacters.Contains(code[i]) == false) {
          return false;
        }
      }

      return true;
    }

    //파티 코드의 특정 위치에 지역 인덱스를 삽입
    public virtual string EncodeRegion(string code, int region) {
      if (string.IsNullOrEmpty(code)) {
        return null;
      }

      if (region < 0 || region >= 32) {
        return null;
      }

      if (region >= ValidCharacters.Length) {
        return null;
      }

      var index = Math.Clamp(EncodedRegionPosition, 0, code.Length - 1);

      if (index < 0 || index >= code.Length) {
        return null;
      }

      return code.Remove(index, 1).Insert(index, ValidCharacters[region].ToString());
    }

    // 파티 코드에서 지역 인덱스를 추출
    public virtual int DecodeRegion(string code) {
      if (string.IsNullOrEmpty(code)) {
        return -1;
      }

      var index = Math.Clamp(EncodedRegionPosition, 0, code.Length - 1);

      if (index < 0 || index >= code.Length) {
        return -1;
      }

      return ValidCharacters.IndexOf(code[index]);
    }
  }
}
