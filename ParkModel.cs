using ParkProject.Model.NPCs;
using ParkProject.Model.Objects;
using ParkProject.Persistence;


/// A program alapvetően a MVVM elven épült fel,
/// és ez a File tartalmazza a Model részt


namespace ParkProject.Model
{
    public enum GameSpeed { Normal, Stop }

    public class ParkModel
    {

        /// <summary>
        /// The ParkModel constructor
        /// </summary>
        /// <param name="p"></param>
        /// <param name="data"></param>
        public ParkModel(ParkData p, FileDataAccess data)
        {
            fileDataAccess = data;
            park = p;
            park.money = 100000;
            speed = GameSpeed.Stop;
        }

        /// <summary>
        /// The mouse action enumerator
        /// </summary>
        public enum mouseAction
        {
            Game1, Game2, Game3, Game4,
            Restaurant1, Restaurant2, Restaurant3, Restaurant4,
            Restroom1, Restroom2,
            Road,
            Plant1, Plant2,
            Empty,
            Entrance,
        }

        /// <summary>
        /// If player is below losermoney it is Game Over.
        /// </summary>
        public int losermoney = -20000;
        /// <summary>
        /// Game Over call to Form
        /// </summary>
        public event Action GameOverEvent;
        public event Action<Tile> ObjectBrokeEvent;
        public event Action<Tile> RepairEvent;
        public FileDataAccess fileDataAccess;
        public ParkData park;
        public GameSpeed speed;
        private int npcSpawnTimer = 10;
        private int costTimer = 0;
        private int npcIdCounter = 0;
        //private DataAcces gameDataAccess;

        #region Game handling

        /// <summary>
        /// Caclualtes, the reputation of the park, from guest happiness.
        /// </summary>
        private void reputationCalc()
        {
            int temp = 0;
            for (int i = 0; i < park.Guests.Count(); i++)
            {
                temp += park.Guests[i].happiness;
            }
            if (temp != 0 && park.Guests.Count() != 0)
                park.rep = (int)(temp / park.Guests.Count());
        }

        /// <summary>
        /// Removes the maintenance cost.
        /// </summary>
        public void keepUpCost()
        {
            if (!(costTimer == 60))
            {
                costTimer++;
                return;
            }
            costTimer = 0;
            int cost = 0;

            park.Games.FindAll(ent => ent.status == ObjectStatus.Ready).ForEach(ent => cost += (int)Math.Round((double)ent.price / 10));
            park.Restaurants.FindAll(ent => ent.status == ObjectStatus.Ready).ForEach(ent => cost += (int)Math.Round((double)ent.price / 10));
            park.Restrooms.FindAll(ent => ent.status == ObjectStatus.Ready).ForEach(ent => cost += (int)Math.Round((double)ent.price / 10));
            //egyenlőle az árakat még nem találtuk ki.

            park.money -= cost;

            if (park.money < losermoney)
            {
                //GameOverEvent();
            }
        }

        /// <summary>
        /// Called by the timer's tick, this brings the game forward.
        /// </summary>
        public void proceedGame()
        {
            if (speed != GameSpeed.Stop)
            {
                npcSpawn();
                reputationCalc();
                NPCMetersCasual();
                PlantCheck();
                GameCheck();
                RestaurantCheck();
                RestroomCheck();
            }
            npcAction();
        }

        #endregion

        #region Load/Save

        /// <summary>
        /// Egy játékállás betöltése
        /// </summary>
        public async Task<ParkData> LoadPark(String path)
        {
            park.NewGame();
            park = await fileDataAccess.LoadAsync(path);
            //park.onLoad(await fileDataAccess.LoadAsync(path));
            return park;
        }

        /// <summary>
        /// Egy játékállás mentése
        /// </summary>
        public async Task SavePark(String path, ParkData park)
        {
            await fileDataAccess.SaveAsync(path, park);
        }

        #endregion

        #region NPC handling

        /// <summary>
        /// Gives an NPC a road to follow, in a list of Tiles, needs a destination tile, and a location tile.
        /// Uses the 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="npcL"></param>
        void npcPathfind(Guest g)
        {
            if (park._graph == null)
            {
                return;
            }
            newDest(g);
            if (g.status != NPCStat.Idle)
            {
                g.path = park._graph.BellmanFordAlg(g.position, g.target);
            }
        }

        /// <summary>
        /// Adds a new NPC to the game, sets its location, to the
        /// entrance, gives it a new path
        /// </summary>
        public void npcSpawn()
        {
            if (npcSpawnTimer == 10)
            {
                park.Guests.Add(new Guest(npcIdCounter));
                npcPathfind(park.Guests.Last());
                park.money += 50;
                npcSpawnTimer = 0;
                return;
            }
            npcSpawnTimer++;
            Random rnd = new Random();
            int p = rnd.Next(1, (101 - (int)park.rep));
            if (p == 1)
            {
                park.Guests.Add(new Guest(npcIdCounter));
                npcPathfind(park.Guests.Last());
                park.money += 50;
            }
            return;
        }

        public void janitorSpawn(int jID)
        {
            bool found = false;
            int i = 0;
            while (!found)
            {
                if (park.Employees[i].id == jID)
                    found = true;
                else
                    i++;

            }
            park.Employees[i].position = park.entranceTile;
            JanitorNewDest((park.Employees.Count) - 1);
        }

        /// <summary>
        /// Adds a janitor to the park
        /// </summary>
        public void buyJanitor()
        {
            park.Employees.Add(new Employee(npcIdCounter));
            park.money -= 2000;
            janitorSpawn(park.Employees.Last().id);
        }

        /// <summary>
        /// Puts the janitor on the map, wiith a destination and a path to the given tile
        /// </summary>
        public int sendJanitor(Tile dest, int destID)
        {
            if (park.Employees.Count == 0)
            {
                return -1;
            }
            int i = 0;
            while (i < park.Employees.Count && park.Employees[i].status != NPCStat.Walking)
            {
                i++;
            }

            if (i == park.Employees.Count)
            {
                return -1;
            }

            if (Equals(park.Employees[i].position, new Tile(-1, -1)))
            {
                park.Employees[i].position = park.entranceTile;
            }
            park.Employees[i].targetID = destID;
            park.Employees[i].path = park._graph.BellmanFordAlg(park.Employees[i].position, park.giveEnterances(dest)[0]);
            park.Employees[i].target = park.Employees[i].path.Last();
            if (park.Employees[i].path != null)
            {
                park.Employees[i].status = NPCStat.ToRepair;
            }
            return i;
        }

        /// <summary>
        /// Sends the janitor to a random location
        /// </summary>
        public void JanitorNewDest(int jID)
        {
            Random rand = new Random();
            bool found = false;
            int k = 0;
            Tile tmp = new Tile();
            while (!found)
            {
                k = rand.Next(0, 2);
                switch (k)
                {
                    case 0:
                        if (park.Games.Count == 0)
                            break;
                        k = rand.Next(0, park.Games.Count());
                        if (park.isConnected(park.Games[k].position))
                        {
                            found = true;
                            park.Employees[jID].target = park.giveEnterances(park.Games[k].position)[0];
                            park.Employees[jID].targetID = -1;
                            park.Employees[jID].path = park._graph.BellmanFordAlg(park.Employees[jID].position, park.Employees[jID].target);
                            park.Employees[jID].status = NPCStat.Walking;
                        }
                        break;
                    case 1:
                        if (park.Restaurants.Count == 0)
                            break;
                        k = rand.Next(0, park.Restaurants.Count());
                        if (park.isConnected(park.Restaurants[k].position))
                        {
                            found = true;
                            park.Employees[jID].target = park.giveEnterances(park.Restaurants[k].position)[0];
                            park.Employees[jID].targetID = -1;
                            park.Employees[jID].path = park._graph.BellmanFordAlg(park.Employees[jID].position, park._graph.vertices[k].v);
                            park.Employees[jID].status = NPCStat.Walking;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Moves NPC's on the map, according to their status.
        /// IT'S NOT HANDELING PLAYING NPCs
        /// </summary>
        void npcAction()
        {
            foreach (Employee emp in park.Employees)
            {
                if ((emp.status == NPCStat.ToRepair && emp.path.Count != 0) || (emp.status == NPCStat.Walking && emp.path.Count > 1))
                {
                    emp.position = emp.path[0];
                    emp.path.Remove(emp.path[0]);
                }
                else if (emp.status == NPCStat.Idle || (emp.status == NPCStat.Walking && (emp.path.Count == 1 || emp.path.Count == 0)))
                {
                    JanitorNewDest(emp.id);
                }
            }

            for (int i = 0; i < park.Guests.Count; i++)
            {
                if (park.Guests[i].status == NPCStat.Idle)
                {
                    npcPathfind(park.Guests[i]);
                    if (park.Guests[i].path != null && park.Guests[i].status != NPCStat.Out)
                    {
                        park.Guests[i].status = NPCStat.Walking;
                    }
                }
                if (park.Guests[i].status == NPCStat.Walking)
                {
                    if (park.Guests[i].path.Count == 1)
                    {
                        park.Guests[i].position = park.Guests[i].path[0];
                        park.Guests[i].status = NPCStat.Waiting;
                        putNPConObject(park.Guests[i].targetID, park.Guests[i]);
                    }
                    else
                    {
                        park.Guests[i].position = park.Guests[i].path[0];
                        park.Guests[i].path.Remove(park.Guests[i].path[0]);
                    }
                }

                else if (park.Guests[i].status == NPCStat.Out)
                {
                    if (park.Guests[i].path.Count == 0)
                    {
                        park.RemoveGuest(park.Guests[i]);
                    }
                    else
                    {
                        park.Guests[i].position = park.Guests[i].path[0];
                        park.Guests[i].path.Remove(park.Guests[i].path[0]);
                    }
                }
            }
        }

        /// <summary>
        /// Adds the NPC to the waiting list of an Object
        /// </summary>
        /// <param name="id"></param>
        /// <param name="v"></param>
        public void putNPConObject(int id, Guest v)
        {
            Object tmp = park.searchObject(id);
            switch (tmp.objectType)
            {
                case 'g':
                    Game g = (Game)tmp;
                    g.waiting.Add(v.id);
                    break;
                case 'r':
                    Restaurant r = (Restaurant)tmp;
                    r.waiting.Add(v.id);
                    break;
                case 'w':
                    Restroom R = (Restroom)tmp;
                    R.waiting.Add(v.id);
                    break;
                default:
                    break;
            }
        }


        /// <summary>
        /// General mood changes for NPC (meant to be bonded to a timer)
        /// </summary>
        public void NPCMetersCasual()
        {
            //General happiness decrease
            park.Guests.FindAll(ent => ent.status == NPCStat.Waiting && ent.happiness > 2).ForEach(ent => ent.happiness -= 2);
            park.Guests.FindAll(ent => ent.status == NPCStat.Idle || ent.status == NPCStat.Walking && ent.happiness > 2).ForEach(ent => ent.happiness--);
            //Sadder if hungry
            park.Guests.FindAll(ent => ent.hunger > 60 && ent.happiness > 2).ForEach(ent => ent.happiness--);
            //Sadder if needs toilet
            park.Guests.FindAll(ent => ent.toilet > 60 && ent.happiness > 2).ForEach(ent => ent.happiness--);
            //General hunger incrase
            park.Guests.FindAll(ent => ent.hunger < 100).ForEach(ent => ent.hunger++);
            //General toilet incrase
            park.Guests.FindAll(ent => ent.toilet < 100).ForEach(ent => ent.toilet++);

        }

        /// <summary>
        /// Manages the games and NPCs
        /// </summary>
        void GameCheck()
        {
            List<Guest> guests = new List<Guest>();
            foreach (Game ent in park.Games)
            {
                if (ent.status == ObjectStatus.Broken)
                {
                    foreach (Guest g in park.Guests)
                    {
                        if (g.targetID == ent.id)
                        {
                            npcPathfind(g);
                        }
                        ent.waiting = new List<int>();
                    }
                    int j = sendJanitor(ent.position, ent.id);
                    if (ent.janitor == -1)
                    {
                        ent.janitor = j;
                    }
                    else if (j != -1)
                        ent.status = ObjectStatus.Repairing; //Azért, hogy az épület, amihez már kiküldtük a javítót, ne legyen mégegyszer meghívva
                }
                else if (ent.status == ObjectStatus.Repairing)
                    OnRepair(ent, ent.janitor);
                else if ((ent.status != ObjectStatus.Broken || ent.status != ObjectStatus.Repairing) && ent.waiting.Count>=1 && ent.waiting.Count >= (int)(ent.serveCapacity * (double)(ent.minUsage / 100.00)))
                {

                    for (int i = 0; i < (int)(ent.serveCapacity * (double)(ent.minUsage / 100.00)); i++)
                    {
                        guests.Add(park.Guests.Find(x => x.id == ent.waiting.ElementAt(0)));
                    }
                    if (guests.Count != 0)
                        OnGame(ent, guests);
                }
            }
        }

        /// <summary>
        /// Uses Task.Delay, to simulate the games, sets the NPCs to Idle
        /// </summary>
        /// <param name="guests"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        public async Task OnGame(Game game, List<Guest> guests)
        {
            if (game.status == ObjectStatus.Repairing || game.status == ObjectStatus.Broken)
            {
                return;
            }
            game.status = ObjectStatus.Playing;
            guests.ForEach(ent => ent.status = NPCStat.Playing);
            game.waiting.RemoveRange(0, guests.Count);
            await Task.Delay(game.playTime * 100);
            game.status = ObjectStatus.Ready;
            guests.ForEach(ent =>
            {
                ent.status = NPCStat.Idle;
                ent.happiness += game.happiness;
                ent.money -= game.ticketPrice;
                park.money += game.ticketPrice;
            });
            game.healthPercent -= 2;
            if (game.healthPercent < 5 && game.status != ObjectStatus.Repairing)
            {
                game.status = ObjectStatus.Broken;
                foreach (int guestId in game.waiting)
                {
                    searchGuest(guestId).status = NPCStat.Idle;
                }

                ObjectBrokeEvent(game.position);
            }

        }

        public async Task OnRepair(Object o, int j)
        {
            if (!Equals(park.Employees[j].position, park.Employees[j].target) || park.Employees[j].status == NPCStat.Repairing)
                return;
            park.Employees[j].status = NPCStat.Repairing;
            await Task.Delay(20000);
            o.healthPercent = 100;
            o.status = ObjectStatus.Ready;
            o.janitor = -1;
            RepairEvent(o.position);
            JanitorNewDest(j);
        }

        public void RestaurantCheck()
        {
            foreach (Restaurant ent in park.Restaurants)
            {
                if (ent.status == ObjectStatus.Broken)
                {
                    foreach (Guest g in park.Guests)
                    {
                        if (g.targetID == ent.id)
                        {
                            npcPathfind(g);
                            if (g.path != null && g.status != NPCStat.Out)
                            {
                                g.status = NPCStat.Walking;
                            }
                        }
                    }
                    int j = sendJanitor(ent.position, ent.id);
                    if (j == -1) { return; }
                    ent.status = ObjectStatus.Repairing; //Azért, hogy az épület, amihez már kiküldtük a javítót, ne legyen mégegyszer meghívva
                }
                else if (ent.status == ObjectStatus.Repairing)
                    OnRepair(ent, ent.janitor);
                else if ((ent.status != ObjectStatus.Broken || ent.status != ObjectStatus.Repairing) && ent.serveCapacity > ent.serving && ent.waiting.Count != 0)
                {
                    OnRestaurant(ent, park.Guests.Find(x => x.id == ent.waiting.ElementAt(0)));
                }
            }
        }
        public async Task OnRestaurant(Restaurant ent, Guest guest)
        {
            if (ent.status == ObjectStatus.Repairing || ent.status == ObjectStatus.Broken)
            {
                return;
            }
            ent.serving++;
            guest.status = NPCStat.Eating;
            ent.waiting.RemoveAt(0);
            await Task.Delay(ent.serveTime);
            guest.status = NPCStat.Idle;
            guest.hunger /= 2;
            guest.money -= ent.foodPrice;
            park.money += ent.foodPrice;
            ent.serving--;
            ent.healthPercent -= 2;
            if (ent.healthPercent < 5 && ent.status != ObjectStatus.Repairing)
            {
                ent.status = ObjectStatus.Broken;
                foreach (int guestId in ent.waiting)
                {
                    searchGuest(guestId).status = NPCStat.Idle;
                }

                ObjectBrokeEvent(ent.position);
            }
        }

        public void RestroomCheck()
        {
            foreach (Restroom ent in park.Restrooms)
            {
                if (ent.status == ObjectStatus.Broken)
                {
                    foreach (Guest g in park.Guests)
                    {
                        if (g.targetID == ent.id)
                        {
                            npcPathfind(g);
                            if (g.path != null && g.status != NPCStat.Out)
                            {
                                g.status = NPCStat.Walking;
                            }
                        }
                    }
                    int j = sendJanitor(ent.position, ent.id);
                    if (j == -1) { return; }
                    ent.status = ObjectStatus.Repairing; //Azért, hogy az épület, amihez már kiküldtük a javítót, ne legyen mégegyszer meghívva
                }
                else if (ent.status == ObjectStatus.Repairing)
                    OnRepair(ent, ent.janitor);
                else if ((ent.status != ObjectStatus.Broken || ent.status != ObjectStatus.Repairing) && ent.serveCapacity > ent.serving && ent.waiting.Count != 0)
                {
                    OnRestRoom(ent, park.Guests.Find(x => x.id == ent.waiting.ElementAt(0)));
                }
            }
        }

        public async Task OnRestRoom(Restroom ent, Guest guest)
        {
            if (ent.status == ObjectStatus.Repairing || ent.status == ObjectStatus.Broken)
            {
                return;
            }
            ent.serving++;
            guest.status = NPCStat.Out;
            await Task.Delay(ent.looTime);
            guest.status = NPCStat.Idle;
            guest.toilet = 0;
            guest.money -= ent.ticketPrice;
            park.money += ent.ticketPrice;
            ent.serving--;
            ent.healthPercent -= 2;
            if (ent.healthPercent < 5 && ent.status != ObjectStatus.Repairing)
            {
                ent.status = ObjectStatus.Broken;
                ObjectBrokeEvent(ent.position);
            }
        }
        /// <summary>
        /// Does the happiness increase to NPC-s
        /// </summary>
        /// <returns></returns>
        public async Task PlantCheck()
        {

            foreach (Plant ent in park.Plants)
            {
                for (int i = ent.position.x - ent.radius; i < ent.position.x + ent.radius + ent.size.height; i++) //height and width may be switched up
                {
                    for (int j = ent.position.y - ent.radius; j < ent.position.y + ent.radius + ent.size.width; j++)
                    {
                        park.Guests.FindAll(x => x.position.x == i && x.position.y == j && x.happiness < 80).ForEach(x => x.happiness += ent.happiness);
                    }
                }
            }
        }


        /// <summary>
        /// Gives an NPC a new destination
        /// </summary>
        /// <param name="n"></param>
        void newDest(Guest n)
        {
            Random r = new Random();
            int p = r.Next(1, Math.Abs(101 - n.hunger));
            if (n.money <= 0 || n.happiness <= 0)
            {
                n.target = park.entranceTile;
                n.targetID = 1;
                n.status = NPCStat.Out;
                return;
            }
            /// p is for probability
            if (n.hunger > 30 && p == 1)
            {
                for (int i = 0; i < park.Restaurants.Count; i++)
                {
                    p = r.Next(0, park.Restaurants.Count());
                    if (park.isConnected(park.Restaurants[p].position) && park.Restaurants[p].waiting.Count < 20)
                    {
                        n.target = park.giveEnterances(park.Restaurants[p].position)[0];
                        n.targetID = park.Restaurants[p].id;
                        n.status = NPCStat.Walking;
                        return;
                    }
                }
                return;
            }
            else if (n.toilet > 30 && p == 1)
            {
                for (int i = 0; i < park.Restrooms.Count; i++)
                {
                    p = r.Next(0, park.Restrooms.Count());
                    if (park.isConnected(park.Restrooms[p].position) && park.Restrooms[p].waiting.Count < 20)
                    {
                        n.target = park.giveEnterances(park.Restrooms[p].position)[0];
                        n.targetID = park.Restrooms[p].id;
                        n.status = NPCStat.Walking;
                        return;
                    }
                }
                return;
            }
            else
            {
                p = r.Next(0, park.Games.Count());
                for (int i = 0; i < park.Games.Count; i++)
                {
                    if (park.isConnected(park.Games[p].position) && park.Games[p].waiting.Count < 20 && (park.Games[i].status != ObjectStatus.Broken || park.Games[i].status != ObjectStatus.Repairing))
                    {
                        n.target = park.giveEnterances(park.Games[p].position)[0];
                        n.targetID = park.Games[p].id;
                        n.status = NPCStat.Walking;
                        return;
                    }
                }
                return;
            }
        }

        /// <summary>
        /// Searches for a Guest by an ID, idealy returns an Guest,
        /// if Object not found, it returns null.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Guest searchGuest(int id)
        {
            int i = 0;
            while (park.Guests[i].id != id)
            {
                i++;
            }
            return park.Guests[i];
        }

        #endregion

        #region Object handling

        /// <summary>
        /// Épület/Út lehelyezése
        /// </summary>
        public int addNewObject(mouseAction building, Tile position)
        {
            if (speed != GameSpeed.Stop)
                return 1;

            switch (building)
            {
                case mouseAction.Game1:
                    return (park.AddNewGame('a', position));
                case mouseAction.Game2:
                    return (park.AddNewGame('b', position));
                case mouseAction.Game3:
                    return (park.AddNewGame('c', position));
                case mouseAction.Game4:
                    return (park.AddNewGame('d', position));
                case mouseAction.Restaurant1:
                    return (park.AddNewRestaurant('a', position));
                case mouseAction.Restaurant2:
                    return (park.AddNewRestaurant('b', position));
                case mouseAction.Restaurant3:
                    return (park.AddNewRestaurant('c', position));
                case mouseAction.Restaurant4:
                    return (park.AddNewRestaurant('d', position));
                case mouseAction.Restroom1:
                    return (park.AddNewRestroom('a', position));
                case mouseAction.Restroom2:
                    return (park.AddNewRestroom('b', position));
                case mouseAction.Road:
                    return (park.AddNewRoad(position));
                case mouseAction.Plant1:
                    return (park.AddNewPlant('a', position));
                case mouseAction.Plant2:
                    return (park.AddNewPlant('b', position));
                default: return 1;
            }

        }

        /// <summary>
        /// Deletes a Building. (Same as RemoveObject)
        /// </summary>
        /// <param name="pos"></param>
        public (Tile, ObjectSize) DeleteObject(Tile pos)
        {
            if (speed != GameSpeed.Stop)
                return (new Tile(-1, -1), new ObjectSize(0, 0));
            return park.RemoveObject(pos);

        }

        /// <summary>
        /// Damages the Buildings
        /// </summary>
        /*public List<Tile> DamageObjects()
        {
            List<Tile> brokenObjects = new List<Tile>();
            //Dealing the damage
            park.Games.FindAll(ent => ent.status == ObjectStatus.Ready).ForEach(ent => ent.healthPercent -= 2);
            park.Restaurants.FindAll(ent => ent.status == ObjectStatus.Ready).ForEach(ent => ent.healthPercent -= 2);
            park.Plants.FindAll(ent => ent.status == ObjectStatus.Ready).ForEach(ent => ent.healthPercent -= 2);
            park.Restrooms.FindAll(ent => ent.status == ObjectStatus.Ready).ForEach(ent => ent.healthPercent -= 2);



            //Putting Buildings to Broken status
            park.Games.FindAll(ent => ent.healthPercent < 2).ForEach(ent => { ent.status = ObjectStatus.Broken; brokenObjects.Add(ent.position); });
            park.Restaurants.FindAll(ent => ent.healthPercent < 2).ForEach(ent => { ent.status = ObjectStatus.Broken; brokenObjects.Add(ent.position); });
            park.Plants.FindAll(ent => ent.healthPercent < 2).ForEach(ent => { ent.status = ObjectStatus.Broken; brokenObjects.Add(ent.position); });
            park.Restrooms.FindAll(ent => ent.healthPercent < 2).ForEach(ent => { ent.status = ObjectStatus.Broken; brokenObjects.Add(ent.position); });

            return brokenObjects;
        }*/

        /// <summary>
        /// Repair function PROTOTYPE
        /// </summary>
        //talán majd await-el kell meghívni? ¯\_(ツ)_/¯
        public async void RepairObject(int Id)
        {
            Object building;
            switch (park.listOfBuildingTypes.ElementAt(Id))
            {
                case BuildingType.Game:
                    building = park.Games.Find(x => x.id == Id);
                    break;
                case BuildingType.Restaurant:
                    building = park.Restaurants.Find(x => x.id == Id);
                    break;
                case BuildingType.Restroom:
                    building = park.Restrooms.Find(x => x.id == Id);
                    break;
                case BuildingType.Plant:
                    building = park.Plants.Find(x => x.id == Id);
                    break;
                case BuildingType.Road:
                    building = park.Roads.Find(x => x.id == Id);
                    break;
                default: return;
            }
            building.status = ObjectStatus.Repairing;
            Thread.Sleep((100 - building.healthPercent) * 100);

            building.healthPercent = 100;
            building.status = ObjectStatus.Ready;
        }

        #endregion

    }
}
