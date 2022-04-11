using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Mug.MemoryAccessing
{
    struct MemoryValue
    {
        public int[] offsets;
        public int size;

        public MemoryValue(int[] offsets, int size)
        {
            this.offsets = offsets;
            this.size = size;
        }
    }
}
