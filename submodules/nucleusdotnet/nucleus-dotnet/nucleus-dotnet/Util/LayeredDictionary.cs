using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus {
    public class LayeredDictionary<T, K> {
        protected List<Dictionary<T, K>> layers;

        public LayeredDictionary()
            : this(new Dictionary<T, K>()) {
        }

        public LayeredDictionary(Dictionary<T, K> first) {
            layers = new List<Dictionary<T, K>>();

            // add first layer
            layers.Add(first);
        }

        public Dictionary<T, K> CurrentLayer {
            get {
                return layers[layers.Count - 1];
            }
        }

        /// <summary>
        /// Returns the count in the top layer
        /// </summary>
        public int Count {
            get {
                return layers[layers.Count - 1].Count;
            }
        }

        public int LayerCount {
            get { return layers.Count; }
        }

        public K this[T key] {
            get {
                K val = default(K);
                for (int i = layers.Count - 1; i >= 0; i--) {
                    var dict = layers[i];

                    if (dict.TryGetValue(key, out val)) {
                        break;
                    }
                }
                return val;
            }
            set {
                int layer = layers.Count - 1;
                var dict = layers[layer];
                dict.Add(key, value);
            }
        }

        public K Get(T key, out int layer) {
            K val = default(K);
            layer = -1;
            for (int i = layers.Count - 1; i >= 0; i--) {
                var dict = layers[i];

                if (dict.TryGetValue(key, out val)) {
                    layer = i;
                    break;
                }
            }
            return val;
        }

        public K Get(int layer, T key) {
            return layers[layer][key];
        }

        public Dictionary<T, K> GetLayer(int layer) {
            return layers[layer];
        }

        public void PushLayer() {
            layers.Add(new Dictionary<T, K>());
        }

        public void PopLayer() {
            layers.RemoveAt(layers.Count - 1);
        }
    }
}
