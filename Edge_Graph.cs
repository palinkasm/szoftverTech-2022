using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParkProject.Model
{
    public class Edge
    {
        public Tile v { get; set; }

        public Edge? next { get; set; }

        public Edge(Tile t)
        {
            v = t;
            next = null;
        }

        public void addNext(Tile t)
        {
            if (next != null)
                addNext(t, next);
            else
                next = new Edge(t);
        }

        public void addNext(Tile t, Edge n)
        {
            if (n.next != null)
                addNext(t, n.next);
            else
                n.next = new Edge(t);
        }

    }

    public class Graph
    {
        public int V = 0; //Vertices (csúcsok száma)
        public int E = 0; //Edges (élek száma)
        public List<Edge> vertices = new List<Edge>();

        int verticePosition(Tile t)
        {
            int i;
            for (i = 0; i < vertices.Count; i++)
            {
                if (Equals(vertices[i].v, t))
                {
                    return i;
                }
            }
            return -1;
            //Exception needed here
        }

        /// <summary>
        /// Adds a vertice(Csúcs) to the graph, if thelis is null,
        /// or empty, it just adds the vertice
        /// </summary>
        /// <param name="t"></param>
        /// <param name="edges"></param>
        public void addVertice(Tile t, List<Tile> edges)
        {
            vertices.Add(new Edge(t));
            if (edges == null || edges.Count == 0)
            {
                V++;
                return;
            }
            for (int i = 0; i < edges.Count; i++)
            {
                vertices.Last().addNext(edges[i]);
                vertices[positionInGraph(edges[i])].addNext(t);
            }
            V++;
            E += edges.Count;
        }

        /// <summary>
        /// Removes a vertice(Csúcs), if the vertice is not present in  the graph
        /// the algorithm will not do anything, it also removes the edges coming
        /// form the vertice.
        /// </summary>
        /// <param name="t"></param>
        public void removeVertice(Tile t)
        {
            int index = verticePosition(t);
            if (index == -1)
            {
                return;
            }
            if (vertices[index].next == null)
            {
                V--;
                vertices.RemoveAt(index);
                return;
            }

            //Élek eltávolítása
            Edge linkedEdges = vertices[index].next; //Az élek amik a csúcshoz tartoznak
            //Edge prev = vertices[positionInGraph(linkedEdges.v)]; //Az előző vizsgált él
            int i = 0;
            while(linkedEdges != null)
            {
                Edge prev = vertices[positionInGraph(linkedEdges.v)]; //Az előző vizsgált él
                Edge nexts = vertices[positionInGraph(linkedEdges.v)].next;
                while (nexts != null)
                {
                    if (Equals(nexts.v, t))
                    {
                        prev.next = nexts.next;
                        nexts = null;
                    }
                    if(nexts != null)
                    {
                        prev = nexts;
                        nexts = nexts.next;
                    }
                }
                i++;
                linkedEdges = linkedEdges.next;
            }
            vertices.RemoveAt(index);
            V--;
            E = E - i;
        }

        /// <summary>
        /// This is the pathfinding algorithm
        /// Returns a List, filled with Tiles,
        /// witch the NPC will follow to it's destination
        /// Uses the Bellman-Ford Algorithm
        /// If no road is avabile, it returns null
        /// </summary>
        /// <param name="start"></param>
        /// <param name="dest"></param>
        /// <param name="g"></param>
        /// <returns></returns>
        public List<Tile> BellmanFordAlg(Tile start, Tile dest)
        {

            if(Equals(start, dest))
            {
                List<Tile> v = new List<Tile>();
                v.Add(start);
                return v;
            }

            ///Setting everithing to default
            int[] d = new int[V];         //A d értéke
            Tile[] pi = new Tile[V];      //A Pi értéke
            bool[] inQ = new bool[V];     //Sorban van-e
            int[] e = new int[V];         //Menet
            for (int i = 0; i < V; i++)
            {
                d[i] = int.MaxValue;
                inQ[i] = false;
                pi[i] = new Tile(-1,-1);
            }

            d[positionInGraph(start)] = 0;
            e[positionInGraph(start)] = 0;

            Queue<Edge> Q = new Queue<Edge>();
            Q.Enqueue(vertices[positionInGraph(start)]);
            inQ[positionInGraph(start)] = true;

            Edge U;
            while (Q.Count != 0)
            {
                U = Q.Dequeue();
                inQ[positionInGraph(U.v)] = false;


                Edge V = U.next;
                while (V != null)
                {
                    if (d[positionInGraph(V.v)] > d[positionInGraph(U.v)] + 1 && d[positionInGraph(U.v)] != int.MaxValue)
                    {
                        d[positionInGraph(V.v)] = d[positionInGraph(U.v)] + 1;
                        pi[positionInGraph(V.v)] = U.v;
                        e[positionInGraph(V.v)] = e[positionInGraph(U.v)] + 1;
                        if (e[positionInGraph(V.v)] < this.V)
                        {
                            if (!inQ[positionInGraph(V.v)])
                            {
                                Q.Enqueue(vertices[positionInGraph(V.v)]);
                                inQ[positionInGraph(V.v)] = true;
                            }
                        }
                    }
                    V = V.next;
                }
            }

            List<Tile> r = new List<Tile>();
            r.Add(pi[positionInGraph(dest)]);
            if (r[0].x == -1 && r[0].y == -1)
            {
                return null;
            }
            int j = 0;
            for (int i = 0; i < pi.Length; i++)
            {
                if (r[j].x == start.x && r[j].y == start.y)
                {
                    r.Insert(0, dest);
                    r.Reverse();
                    return r;
                }
                if (!Equals(pi[positionInGraph(r.Last())], new Tile(-1, -1)))
                {
                    r.Add(pi[positionInGraph(r[r.Count - 1])]);
                    j++;
                }
            }

            return null; ///If no road is avabile, it returns null
        }

        int positionInGraph(Tile t)
        {
            int i = 0;
            while (!vertices[i].v.Equals(t))
            {
                i++;
            }
            return i;
        }

    }
}