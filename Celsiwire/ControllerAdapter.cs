using System;

namespace Controller
{
    public class Adapter
    {
        private static void Detect(Configuration.Port port)
        {
            /* Poskusi, če je vmesnik DS9097E in potem še DS9097U. */
            port.inUse = false;
            try
            {
                using (owAdapter.DS9097E serialAdapter = new owAdapter.DS9097E(port.Serial))
                {
                    serialAdapter.Connect();
                    serialAdapter.owReset();
                }
                port.type = Configuration.Types.DS9097E;
                return;
            }
            catch (UnauthorizedAccessException)
            {
                port.inUse = true;
                return;
            }
            catch
            {

            }

            try
            {
                using (owAdapter.DS9097U serialAdapter = new owAdapter.DS9097U(port.Serial))
                {
                    serialAdapter.Connect();
                    serialAdapter.owReset();
                }
                port.type = Configuration.Types.DS9097U;
                return;
            }
            catch (UnauthorizedAccessException)
            {
                port.inUse = true;
            }
            catch
            {

            }
        }

        private static owAdapter.owAdapterSerial Get(Configuration.Port port)
        {
            if (port.type == Configuration.Types.Present || port.type == Configuration.Types.None)
            {
                Detect(port);
            }

            switch (port.type)
            {
                case Configuration.Types.DS9097E:
                    return new owAdapter.DS9097E(port.Serial);
                case Configuration.Types.DS9097U:
                    return new owAdapter.DS9097U(port.Serial);
                default:
                    port.type = Configuration.Types.None;
                    return null;
            }
        }

        private static bool TryResetAndConnect(Configuration.Port port)
        {
            if (port.serialAdapter == null)
            {
                return false;
            }

            try
            {
                port.serialAdapter.Connect();
            }
            catch (Exception e)
            {
                port.inUse = e is UnauthorizedAccessException;
                Disconnect(port);
                port.type = Configuration.Types.None;
                return false;
            }
            port.inUse = false;

            try
            {
                port.serialAdapter.owReset();
            }
            catch (Exception)
            {
                Disconnect(port);
                port.type = Configuration.Types.Present;
                return false;
            }

            return true;
        }

        public static bool Connect(Configuration.Port port)
        {
            port.serialAdapter = Get(port);
            return TryResetAndConnect(port);
        }

        public static void Disconnect(Configuration.Port port)
        {
            if (port.serialAdapter == null)
            {
                return;
            }
            try
            {
                if (port.serialAdapter.IsOpen)
                {
                    port.serialAdapter.Disconnect();
                }
            }
            catch (Exception e)
            {
                Utils.Log.Warn("{0}: closing failed: {1}", port, e.Message);
            }
            port.serialAdapter = null;
        }

    }
}
