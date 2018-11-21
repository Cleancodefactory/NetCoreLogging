using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NLog;

namespace Ccf.Ck.Libs.Logging
{
    public class KraftLoggerProvider : ILoggerProvider
    {
        readonly Logger _Logger;
        readonly IHttpContextAccessor _Accessor;
        public KraftLoggerProvider(Logger logger, IHttpContextAccessor accessor)
        {
            _Logger = logger;
            _Accessor = accessor;
        }
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return new KraftLogger(_Logger, _Accessor, null);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~KraftLoggerProvider() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
