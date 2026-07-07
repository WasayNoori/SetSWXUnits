using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SetSWXUnits
{
    internal class SolidworksSingleton
    {
        private static SolidWorks.Interop.sldworks.SldWorks swapp;

        internal static SolidWorks.Interop.sldworks.SldWorks getApplication()
        {
            if (swapp == null)
            {
                swapp = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")) as SolidWorks.Interop.sldworks.SldWorks;
                swapp.Visible = true;
                return swapp;

            }

            return swapp;
        }


        internal static void Dispose()


        {
            if (swapp != null)
            {
                swapp.ExitApp();
                swapp = null;
            }
        }
    }
}
