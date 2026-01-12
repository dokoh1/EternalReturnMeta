using System.Threading;

public static class Utility 
{
   public static void RefreshToken(ref CancellationTokenSource _cts)
   {
      if (_cts != null)
      {  // 토큰이 취소요청 했다면
         if (!_cts.IsCancellationRequested)
         {
            _cts.Cancel();
         }
         _cts.Dispose();
      }
      
      _cts = new CancellationTokenSource();
   }
   
}
