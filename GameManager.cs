using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.PostProcessing;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

[RequireComponent(typeof(PhotonView))]
public class GameManager : MonoBehaviour //[CLEANED]
{
    #region Variables
    #region Debugging
    [Space]
    [Header("-----Debugging-----")]
    [SerializeField] TextMeshProUGUI processCountDisplay; //Network Sync Counter Display
    #endregion

    #region Scene Objects  
    [Space]
    [Header("-----Scene Objects-----")]
    [SerializeField] public Canvas mainCanvas;
    [SerializeField] public RectTransform opponentHolder;
    [SerializeField] PostProcessVolume volume;
    [Space]
    [Header("-----Win Objects-----")]
    [SerializeField] GameObject unoWin;
    [SerializeField] GameObject btnRematch;
    [SerializeField] GameObject btnMainMenu;
    [SerializeField] GameObject winnerPanel;
    [SerializeField] Image winnerPic;
    [SerializeField] TextMeshProUGUI winnerText;
    [Space]
    [Header("-----Gameplay Objects-----")]
    [SerializeField] GameObject header;
    [SerializeField] GameObject colourPicker;
    [SerializeField] GameObject continueButton;
    [SerializeField] GameObject callUnoButton;
    [SerializeField] GameObject callOutButton;
    [SerializeField] GameObject callHeader;
    [SerializeField] RectTransform drawPile;
    [SerializeField] RectTransform trashPile;
    [SerializeField] Image bgColour;
    [SerializeField] Image direction;
    [SerializeField] Button leaveButton;
    [Space]
    [Header("-----Lobby Objects-----")]
    [SerializeField] RectTransform lobbyHolder;
    [SerializeField] GameObject lobbyPanel;
    [SerializeField] TextMeshProUGUI roomCode;
    [SerializeField] Button btn_FillBots;
    [SerializeField] int lobbyID;
    #endregion

    #region Movement & Synchronization Variables
    [Space]
    [Header("-----Movement and Sync-----")]
    [Space] [Range(0.2f, 6.2f)] [SerializeField] float moveTime = 6f;
    [Space] [Range(1f, 3f)] [SerializeField] float winFadeTime = 3f;
    [SerializeField] int processCount = 0;
    [SerializeField] string lastSyncingPlayer = "";
    #endregion

    #region Game Data
    [Space]
    [Header("-----Last Card Data-----")]
    [SerializeField] Color lastColour;
    [SerializeField] int lastNumber;
    [Space]
    [Header("-----Player Data-----")]
    public List<Player> players = new List<Player>();
    public Player localPlayer;
    [SerializeField] Player currentPlayer;
    [SerializeField] RectTransform localLobbyPlayer;
    [Space]
    [Header("-----Game Data-----")]
    public int cycleCount = 1;
    public int plusCards = 0;
    [SerializeField] int playCount = 0;
    [SerializeField] int startingCardCount = 7;
    [SerializeField] int botCount = 0;
    [SerializeField] bool called = false;
    [SerializeField] bool drawing = false;
    #endregion

    #region Prefabs
    [Space]
    [Header("-----Preloaded Objects-----")]
    [SerializeField] Card baseCard;
    [SerializeField] GameObject dummyCard;
    [SerializeField] GameObject localHand;
    public GameObject handCover;
    #endregion

    #region cardSprites and cardColours
    [Space]
    [Header("-----Preloaded Card Data-----")]
    [SerializeField] Sprite[] redCardSprites;
    [SerializeField] Sprite[] yellowCardSprites;
    [SerializeField] Sprite[] greenCardSprites;
    [SerializeField] Sprite[] blueCardSprites;
    [SerializeField] Sprite[] specialCardSprites;
    [SerializeField] Color[] customColours = new Color[5];
    Sprite[][] cardSprites;
    Color[] cardColours = new Color[] { Color.red, Color.yellow, Color.green, Color.blue, Color.black };
    #endregion
    #endregion

    #region Instance Singleton, Initialization and Properties
    public static GameManager mainManager;
    public static GameManager Instance { get { return mainManager; } }
    public int ProcessCount { get { return processCount; } }
    public int BotCount { get { return botCount; } }
    public bool IsDrawing { get { return drawing; } set { drawing = value; } }
    private void Awake()
    {
        if (mainManager != null && mainManager != this)
            Destroy(this.gameObject);
        else
            mainManager = this;

        mainCanvas = GameObject.FindGameObjectWithTag("MainCanvas").GetComponent<Canvas>();
        cardSprites = new Sprite[][] { redCardSprites, yellowCardSprites, greenCardSprites, blueCardSprites, specialCardSprites };
    }
    private void Start()
    {
        //Spawn a lobby player for every client
        roomCode.text = "Join Code: " + PhotonNetwork.CurrentRoom.Name.Substring(5);
        localLobbyPlayer = PhotonNetwork.Instantiate("lobbyPlayer", Vector3.zero, Quaternion.identity).GetComponent<RectTransform>();
        lobbyID = localLobbyPlayer.GetComponent<PhotonView>().ViewID;
        GetComponent<PhotonView>().RPC("SyncLobbyPlayer", RpcTarget.AllBuffered, localLobbyPlayer.GetComponent<PhotonView>().ViewID, PlayerManager.Instance.localProfile.spriteIndex, PlayerManager.Instance.localProfile.userName, false, false);
    }
    private void Update()
    {
        if (processCountDisplay.text != ("Process Count " + processCount)) //Update Debugging Text
            processCountDisplay.text = ("Process Count " + processCount);

        if (lobbyPanel.activeSelf) //If all players are ready, start game
        {
            if (players.Count == PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                PhotonNetwork.CurrentRoom.IsVisible = false;
                NetworkStartGame(startingCardCount);
            }
        }
    }
    #endregion

    #region Network Wrappers (Calls RPCs such as draw card)
    public void NetworkReadyPlayer() //Spawns opponent player for everyone else and hand for yourself (Calls once per player at start) [DONE]
    {
        //Spawn Player Object for all other clients 
        GameObject opponentObj = PhotonNetwork.Instantiate("OpponentPlayerBase", Vector3.zero, Quaternion.identity);
        Player opponent = opponentObj.GetComponentInChildren<Player>();
        UserProfile localProfile = PlayerManager.Instance.localProfile;
        opponentObj.transform.SetAsLastSibling();
        opponent.GetComponent<PhotonView>().RPC("InitPlayer", RpcTarget.OthersBuffered, localProfile.userName, localProfile.spriteIndex, localProfile.colIndex, false);

        //Spawn local player for yourself (only your hand)
        Player hand = Instantiate(localHand, mainCanvas.transform, false).GetComponent<Player>();
        hand.InitHandCover();
        localPlayer = hand;
        players.Add(hand);
        hand.name = localProfile.userName;
        hand.playerName = localProfile.userName;
        hand.userPic = PlayerManager.Instance.characters[localProfile.spriteIndex];
        lobbyPanel.transform.SetAsLastSibling();

        //Sync to all clients that you are ready ti okay
        GetComponent<PhotonView>().RPC("SyncLobbyPlayer", RpcTarget.AllBuffered, lobbyID, 0, "", true, false);
        if (players.Count == 1)
            GetComponent<PhotonView>().RPC("ChangeCurrentPlayer", RpcTarget.AllBuffered, 0, 0);
    }
    public void NetworkSpawnBots(int count) //Spawns a synchronized bot player over the network [DONE]
    {
        GetComponent<PhotonView>().RPC("SpawnBot", RpcTarget.MasterClient, count);
    }
    public void NetworkCyclePlayers(bool cont = false) //Cycles players over network. Called by all clients but only executed by host [DONE]
    {
        drawPile.GetComponent<Button>().interactable = false;
        if (cont)
        {
            continueButton.SetActive(false);
            currentPlayer.playerCover.GetComponent<Image>().enabled = true;
            callOutButton.SetActive(false);
            GetComponent<PhotonView>().RPC("ForceCyclePlayers", RpcTarget.MasterClient);
        }
        else if (PhotonNetwork.IsMasterClient)
            GetComponent<PhotonView>().RPC("ForceCyclePlayers", RpcTarget.MasterClient);
    }
    public void NetworkDrawCard(bool syncProcess, bool startGame = false) //Draw the same Card on every client to one player (Called once by local player) [DONE]
    {
        int index = Random.Range(0, 108);
        int col = Random.Range(0, 4);
        int num = Random.Range(1, 9);

        if (startGame)
            index = Random.Range(32, 108);
        GetComponent<PhotonView>().RPC("DrawCard", RpcTarget.All, syncProcess, index, num, col, startGame);
    }
    public void NetworkStartGame(int number = 1) //Starts the game for every client over network [DONE]
    {
        GetComponent<PhotonView>().RPC("ReadyBoard", RpcTarget.All); //Enables turn header and disables lobbyPanel over network
        PhotonNetwork.SendAllOutgoingCommands();
        GetComponent<PhotonView>().RPC("StartGame", RpcTarget.MasterClient, number); //Starts game only once using master client 
    }
    public void NetworkSyncProcess(int count, string sender = "", RpcTarget target = RpcTarget.All) //Adds count to process count and updates last syncingPlayer for each specified client [DONE]
    {
        GetComponent<PhotonView>().RPC("SyncProcess", target, count, sender);
        PhotonNetwork.SendAllOutgoingCommands();
    }
    public void NetworkSyncPlayer(int index, int pCount = 1) //Syncs current player over all clients, pCount is how much to change Process Count by [DONE]
    {
        GetComponent<PhotonView>().RPC("ChangeCurrentPlayer", RpcTarget.All, index, pCount);
    }
    public void NetworkPlayerTurn() //Start Turn for current player over network [DONE]
    {
        GetComponent<PhotonView>().RPC("SyncPlayerTurn", RpcTarget.All);
        PhotonNetwork.SendAllOutgoingCommands();
    }
    public void NetworkSetColour(int colIndex) //Changes last played colour over network [DONE]
    {
        GetComponent<PhotonView>().RPC("ColourChange", RpcTarget.All, colIndex);
        PhotonNetwork.SendAllOutgoingCommands();
    }
    public void NetworkRematch() //Restarts game if all players choose to [DONE]
    {
        GetComponent<PhotonView>().RPC("Rematch", RpcTarget.All);
    }
    public void NetworkLeaveMatch() //Removes local player from game manager and returns to menu [DONE]
    {
        GetComponent<PhotonView>().RPC("LeaveMatch", RpcTarget.All, players.IndexOf(localPlayer));
    }
    public void NetworkCall(int call) //Calls uno or calls out another player over the network [DONE]
    {
        GetComponent<PhotonView>().RPC("CallPlayer", RpcTarget.All, call);
    }
    public void NetworkAllowCall() //Enables call out button for everyone over the network [DONE?]
    {
        GetComponent<PhotonView>().RPC("EnableCall", RpcTarget.All);
        PhotonNetwork.SendAllOutgoingCommands();
    }
    #endregion

    #region Coroutine Wrappers (Calls internal coroutines)
    [PunRPC]
    public void StartGame(int num = 1) //Called only to master client [DONE]
    {
        StartCoroutine(GameStart(num));
    }
    [PunRPC]
    public void DrawCard(bool sync, int cardVal = -1, int number = -1, int colour = -1, bool start = false) //Called on all clients through master [DONE]
    {
        StartCoroutine(Draw(sync, cardVal, number, colour, start));
    }
    public void Discard(Card card) //Called on all clients through card [DONE]
    {
        playCount++;
        card.transform.SetParent(mainCanvas.transform);
        card.UpdateGraphics();
        currentPlayer.RemoveCard(card);
        card.transform.localPosition = new Vector3(card.transform.localPosition.x, card.transform.localPosition.y + 20);
        StartCoroutine(MoveCard(false, card, card.GetComponent<RectTransform>(), trashPile.transform.localPosition, RandomRotation(), 1, Vector3.one));
    }
    [PunRPC]
    public void ForceCyclePlayers() //Called by Master client once at a time [DONE]
    {
        StartCoroutine(CyclePlayers());
    }
    [PunRPC]
    public void CallPlayer(int c) //Wrapper for a player to call UNO [DONE]
    {
        StartCoroutine(Call(c));
    }
    #endregion

    #region Coroutine Methods
    IEnumerator GameStart(int count) //Called on master client ||MUST UPDATE DATA FOR EVERYONE ELSE|| MUST WAIT UNTILL DONE ON EVERY CLIENT [DONE]
    {
        yield return new WaitForSecondsRealtime(1f);
        NetworkDrawCard(false, true);
        yield return new WaitUntil(() => lastNumber != -1);
        yield return new WaitForSecondsRealtime(1f);

        NetworkSyncProcess(-processCount, "master"); //Reset Process count after spawning players

        for (int i = 0; i < players.Count; i++)//Draws [count] cards to each player
        {
            NetworkSyncPlayer(i); //SYNCS CURRENT PLAYER
            yield return new WaitUntil(() => processCount == players.Count - botCount); //Waits till all clients have changed current player
            NetworkSyncProcess(-processCount, "master"); //Reset Process count after spawning players

            for (int d = 0; d < count; d++)
            {
                NetworkDrawCard(true);//Draws same card to current player on all clients
                yield return new WaitUntil(() => processCount == players.Count - botCount); //Waits for card to be drawn on all clients
                NetworkSyncProcess(-processCount, "master"); //Reset Process count after spawning players
            }
        }
        
        NetworkCyclePlayers();//Cycle to the first client and start their turn over network
    }
    IEnumerator Draw(bool syncing, int cardIndex = -1, int num = -1, int col = -1, bool start = false) // Draws and Generates same card on all clients [DONE] 
    {
        //Draw Card GameObject
        Card newCard = Instantiate(baseCard, mainCanvas.transform, false) as Card;
        Transform dummie = null;
        Transform dummiePlaceHolder = null;

        newCard.transform.SetParent(mainCanvas.transform, false);

        #region CardGeneration

        #region Card Placement Logic
        if (!start) //If drawing for player
        {
            dummie = Instantiate(baseCard, currentPlayer.transform, false).transform;

            if (cardIndex == -1)
                cardIndex = Random.Range(0, 108);
            if (col == -1)
                col = Random.Range(0, 4);
          
            currentPlayer.UpdateHandSpacing();
            drawPile.GetComponent<Button>().interactable = false;
            dummie.localScale = currentPlayer.cardScale;
            yield return new WaitForEndOfFrame();

            dummie.SetParent(mainCanvas.transform, true);
            dummiePlaceHolder = Instantiate(dummyCard, currentPlayer.transform, false).transform;
            dummiePlaceHolder.localScale = currentPlayer.cardScale;

            currentPlayer.UpdateHandSpacing();           
        }
        #endregion

        #region CardTypeGeneration
        if (cardIndex < 4)//+4
        {
            newCard.type = 0;
            num = 14;
            col = 4;
        }
        else if (cardIndex < 8)//colour change
        {
            newCard.type = 1;
            num = 13;
            col = 4;
        }
        else if (cardIndex < 16)//skip with random colour
        {
            newCard.type = 2;
            num = 12;
        }
        else if (cardIndex < 24)//reverse with random colour
        {
            newCard.type = 3;
            num = 11;
        }
        else if (cardIndex < 32)//+2 with random colour
        {
            newCard.type = 4;
            num = 10;
        }
        else if (cardIndex < 36)//0 with random colour
        {
            newCard.type = 5;
            num = 0;
        }
        else//random number from 1 to 9 with random colour
        {
            newCard.type = 5;
        }

        newCard.number = num;
        newCard.colour = cardColours[col];
        newCard.graphic = GetCardSprite(num, col);
        #endregion

        newCard.GetComponent<RectTransform>().localPosition = drawPile.localPosition; //Card Start Position
        newCard.GetComponent<Image>().color = new Color(255, 255, 255, 255);
        #endregion


        if(!start)
        {
            StartCoroutine(MoveCard(syncing, newCard, newCard.GetComponent<RectTransform>(), dummie.localPosition, dummie.localRotation, 0, currentPlayer.cardScale, dummiePlaceHolder.gameObject));
            Destroy(dummie.gameObject);
        }
        else
            StartCoroutine(MoveCard(syncing, newCard, newCard.GetComponent<RectTransform>(), trashPile.localPosition, RandomRotation(), 2, new Vector3( 1, 1, 1)));
        
    }
    IEnumerator MoveCard(bool sync, Card card, RectTransform cardTransform, Vector3 dest, Quaternion randomRot, int function, Vector3 endScale, GameObject dummyObj = null) //Moves and plays/finishes draw of card on all clients [DONE]
    {
        #region Card Movement [DONE]
        float t = 0;
        cardTransform.localRotation = Quaternion.Euler(0, 0, 0);
        while (t < moveTime)
        {
            t += Time.deltaTime;
            cardTransform.localPosition = Vector3.Lerp(cardTransform.localPosition, dest, t / moveTime);
            cardTransform.localRotation = Quaternion.Lerp(cardTransform.localRotation, randomRot, t / moveTime);
            cardTransform.localScale = Vector3.Lerp(cardTransform.localScale, endScale, t / moveTime);
            //RefreshLayout(currentPlayer.GetComponent<RectTransform>());
            if (Vector3.Distance(cardTransform.localPosition, dest) < 0.5f)
                break;
            yield return null;
        }
        cardTransform.SetParent(trashPile);
        #endregion  

        #region Card Effect
        switch (function)
        {
            case 0: //Draw(Current Player) [DONE]
                {
                    currentPlayer.UpdateHandSpacing(); //Fix player hand spacing
                    Destroy(dummyObj); //Destroy and replace dummy card with real card
                    cardTransform.SetParent(currentPlayer.transform);
                    cardTransform.localScale = endScale;
                    currentPlayer.AddCard(); //Add card to player's deck and card id holder locally
                    CardHolder.Instance.AddCard(card);

                    card.UpdateGraphics(); //Ready board for next action
                    called = false;
                    if (currentPlayer == localPlayer && plusCards == 0) //Never called by bots 
                    {
                        if (!IsPlayable(currentPlayer.deck[currentPlayer.deck.Count - 1]))
                            drawPile.GetComponent<Button>().interactable = true;

                        if (currentPlayer.deck.Count == 2 && (IsPlayable(currentPlayer.deck[1]) || IsPlayable(currentPlayer.deck[0])) && header.GetComponentInChildren<TextMeshProUGUI>().text != "Starting Game..." && (lastNumber != 14 || lastNumber != 10))//Bots do this check themselves while drawing cards
                            callUnoButton.SetActive(true);
                        else
                            callUnoButton.SetActive(false);
                    }
                                     
                    if (sync) //Syncs draw
                        NetworkSyncProcess(1, localPlayer.name);
                    break;
                }
            case 1: //PlayCard or Discard
                {
                    card.CardEffect();
                    callHeader.SetActive(false);

                    if (currentPlayer.lastDeckSize == 0)
                    {
                        localPlayer.playerCover.GetComponent<Image>().enabled = false;
                        localPlayer.gameObject.SetActive(false);
                        Win();
                        yield break;
                    }

                    if (card.colour == Color.black)
                    {                                             
                        if(currentPlayer == localPlayer)//Enable colour picker locally if player is local
                        {
                            colourPicker.SetActive(true);
                            currentPlayer.playerCover.GetComponent<Image>().enabled = true;                                                   
                        }
                        else if (currentPlayer.botPlayer && PhotonNetwork.IsMasterClient)
                            NetworkSetColour(Random.Range(0, 4));//Bot Randomly chooses a colour here

                        print("Syncing Colour");
                        yield return new WaitUntil(() => lastSyncingPlayer == currentPlayer.name);//Pause Wait for input
                    }
                    else
                        lastColour = card.colour;

                    bgColour.color = customColours[System.Array.IndexOf(cardColours, lastColour)];
                    lastNumber = card.number;

                    for (int i = 0; i < currentPlayer.lastDeckSize; i++)
                    {
                        if(IsPlayable(currentPlayer.deck[i]))
                        {
                            if (currentPlayer == localPlayer) //Your hand
                            {
                                Debug.Log("OPENING HAND");
                                currentPlayer.playerCover.GetComponent<Image>().enabled = false;
                                if (currentPlayer.deck.Count == 2)
                                    callUnoButton.SetActive(true);
                                continueButton.SetActive(true);
                            }
                            else if (PhotonNetwork.IsMasterClient && currentPlayer.botPlayer)
                            {
                                print("Bot has more cards to play");                               
                                currentPlayer.StartBotTurn();//Bot turn, Bots also check for calling uno themselves
                            }
                            lastSyncingPlayer = "";
                            yield break;
                        }
                    }

                    if(players.Count - botCount != 1)
                        leaveButton.interactable = false;
                    continueButton.SetActive(false);
                    callUnoButton.SetActive(false);
                    callOutButton.SetActive(false);                    
                    currentPlayer.playerCover.GetComponent<Image>().enabled = true;
                    playCount = 0;
                    
                    print("Syncing finish drawing cards");

                    print("Finished going through players");
                    if(PhotonNetwork.IsMasterClient)
                    {
                        processCount = 0;
                        print("Master is waiting for process count to be 1 below players.count");
                        while (processCount < (players.Count - botCount) - 1)
                        {
                            NetworkSyncProcess(1, "master");
                            yield return new WaitForSecondsRealtime(0.5f);
                        }
                        yield return new WaitUntil(() => processCount >= (players.Count- botCount) - 1);
                        print("Ended play card on all clients with processCount of " + processCount);
                        NetworkCyclePlayers(); //Only called if master client so they must wait for everyone else to update
                    }
                    break;
                }
            case 2: //Play Starting Card
                {
                    card.UpdateGraphics(); //Show card to players
                    cardTransform.SetParent(trashPile); //Parent to trash pile
                    lastColour = card.colour;
                    bgColour.color = customColours[System.Array.IndexOf(cardColours, card.colour)]; //Set colour and number over 1s             
                    yield return new WaitForSecondsRealtime(1f);
                    lastNumber = card.number;
                    break;
                }
        }
        #endregion
    } 
    IEnumerator CyclePlayers() //Called only on master client. Cycles Players. (Adds cards and skips) [DONE]
    {
        NetworkSyncProcess(-processCount, "master");//Reset Process count for everyone
        print("Cycling " + cycleCount + " Players");

        for (int i = 0; i < cycleCount; i++)
        {
            int index = players.IndexOf(currentPlayer) + 1;

            if (index >= players.Count)
                index = 0;

            print("Syncing Players");
            NetworkSyncPlayer(index); //Syncs current player to new player on all clients, 1 process
            yield return new WaitUntil(() => processCount == players.Count - botCount); //Waits for all players to sync
            print("Resetting Process Count in cycle count");
            NetworkSyncProcess(-processCount, "master");
            yield return new WaitUntil(() => processCount == 0);
        }
        cycleCount = 1;

        if (plusCards > 0) 
        {
            for (int i = 0; i < plusCards; i++)
            {
                print("Drawing plus cards");
                NetworkDrawCard(true);//Draws same card to current player on all clients
                yield return new WaitUntil(() => processCount == players.Count - botCount); //Waits for card to be drawn on all clients
                NetworkSyncProcess(-processCount, "master"); //Reset Process count after spawning players
                print("Drew card to " + currentPlayer.name);
            }
            plusCards = 0;
            print("Finished drawing, cycling to next player");
            yield return new WaitForSecondsRealtime(0.5f);
            NetworkCyclePlayers();
            yield break;
        }
        print("Syncing player turn to " + currentPlayer.name);
        NetworkPlayerTurn();
        yield break;
    }
    #endregion

    #region Private Utilities (Only used by GameManager)
    Quaternion RandomRotation() //Sets random rotation of card  [DONE]
    {
        return Quaternion.Euler(0, 0, Random.Range(-42, 43));
    }
    #endregion

    #region Public Utilities (Used by external objects)
    public Sprite GetCardSprite(int num, int colIndex) //Returns card image based on colour and number/type [DONE]
    {
        if (colIndex == 4)
            return cardSprites[colIndex][num - 13];
		return cardSprites[colIndex][num];
	}
    public bool IsPlayable(Card selection) //Returns the playability of a given card [DONE]
    {
        bool number = (playCount > 0);
        if (!number && (selection.colour == Color.black || selection.colour == lastColour || selection.number == lastNumber))// if card is black or same colour 
            return true;
        else if(number && selection.number == lastNumber)
            return true;
        else
            return false;
    }
    public void LoadNewScene(int index) //Loads Scene by Build Index [DONE]
    {
        SceneManagerLoad.Instance.Load(index);
    }
    #endregion

    #region Network Utilities (Used for syncing over network)
    [PunRPC]
    void ChangeCurrentPlayer(int index, int count) //Change the current player, this is called on every client  [DONE]
    {
        currentPlayer = players[index];
        if (currentPlayer.playerCover == null)
            currentPlayer.InitHandCover();
        NetworkSyncProcess(count); //Updates process count over all clients (This is an individual task per client)
    }
    [PunRPC]
    void SyncProcess(int value, string sender) //Syncs the current process that occured, Called on every client (Adds one to process count from every client each time NetworkSync is called) [DONE]
    {
        processCount += value;
        if (processCount <= 0)
            processCount = 0;
        lastSyncingPlayer = sender;
    }
    [PunRPC]
    void SyncPlayerTurn()//Sync the turn of the current player, called by all clients [DONE]
    {
        int prevIndex = players.IndexOf(currentPlayer) - 1;

        if (prevIndex < 0)
            prevIndex = players.Count - 1;

        continueButton.SetActive(false);
        colourPicker.SetActive(false);
        drawing = false;
        playCount = 0;

        if (callHeader.activeSelf)
            callHeader.SetActive(false);

        if (players[prevIndex].deck.Count == 1 && !called && PhotonNetwork.IsMasterClient) //If call out is possible
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].botPlayer)//Get rid of this and give all bots a chance to network call here
                {
                    int chance = Random.Range(1, 21);
                    print("BOT CALL OUT POSSIBLE");
                    if (chance < 8 && players[i] != players[prevIndex]) //Bots have a 40% chance to call players or other bots out
                    {
                        print("CALL OUT BY BOT");
                        drawPile.GetComponent<Button>().interactable = false;
                        header.GetComponentInChildren<TextMeshProUGUI>().text = currentPlayer.name + "'s Turn!";
                        callOutButton.SetActive(false);
                        NetworkCall(1);
                        return;
                    }
                }
            }
            NetworkAllowCall();
        }
        else
            callOutButton.SetActive(false);

        if (currentPlayer == localPlayer)
        {
            if (players.Count - botCount != 1)
                leaveButton.interactable = false;
            if (currentPlayer.deck.Count == 2)
            {
				for (int i = 0; i < 2; i++)
				{
					if (IsPlayable(currentPlayer.deck[i]))
					{
						callUnoButton.SetActive(true);
						break;
					}
				}				
            }
            else 
                callUnoButton.SetActive(false);         

            Debug.Log("Finishing sync player to myself. Opening hand");
            plusCards = 0;
            currentPlayer.playerCover.GetComponent<Image>().enabled = false;
            drawPile.GetComponent<Button>().interactable = true;
            header.GetComponentInChildren<TextMeshProUGUI>().text = "Your Turn!";
        }       
        else
        {            
            leaveButton.interactable = true;
            if(!currentPlayer.botPlayer)
                callUnoButton.SetActive(false);
            drawPile.GetComponent<Button>().interactable = false;           
            header.GetComponentInChildren<TextMeshProUGUI>().text = currentPlayer.name + "'s Turn!";
            if (currentPlayer.botPlayer && PhotonNetwork.IsMasterClient)
                currentPlayer.StartBotTurn();
        }
    }
    [PunRPC]
    void ColourChange(int choice) //Change the colour, this method is called by everyone [DONE]
    {
        lastColour = cardColours[choice];
        colourPicker.SetActive(false);
        if (currentPlayer == localPlayer && !drawing)
            currentPlayer.playerCover.GetComponent<Image>().enabled = false;
        bgColour.color = customColours[System.Array.IndexOf(cardColours, lastColour)];
        NetworkSyncProcess(0, currentPlayer.name); //Syncs by increasing process count for everyone
    }
    [PunRPC]
    void SyncLobbyPlayer(int id, int spriteIndex, string userName, bool ready = false, bool bot = false) //Sync all the players inside the gameboard's lobby [DONE]
    {
        if (!ready)
        {
            if (bot)
                botCount++;

            for (int i = 1; i < lobbyHolder.transform.childCount; i++)
            {
                if (i >= PhotonNetwork.CurrentRoom.MaxPlayers && lobbyHolder.transform.GetChild(i).GetComponentInChildren<TextMeshProUGUI>().text == "Waiting for Player(s)...")
                    Destroy(lobbyHolder.transform.GetChild(i).gameObject);
            }
            localLobbyPlayer = PhotonView.Find(id).GetComponent<RectTransform>();
            localLobbyPlayer.transform.SetParent(lobbyHolder, false);
            localLobbyPlayer.SetSiblingIndex(PhotonNetwork.CurrentRoom.PlayerCount + botCount);
            localLobbyPlayer.GetComponentsInChildren<Image>()[0].sprite = PlayerManager.Instance.characters[spriteIndex];
            localLobbyPlayer.GetComponentInChildren<TextMeshProUGUI>().text = userName;

            if ((PhotonNetwork.CurrentRoom.PlayerCount + botCount) == PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                btn_FillBots.interactable = false;
                for (int i = 1; i < lobbyHolder.transform.childCount; i++)
                {
                    if (lobbyHolder.transform.GetChild(i).GetComponentInChildren<TextMeshProUGUI>().text == "Waiting for Player(s)...")
                    {
                        Destroy(lobbyHolder.transform.GetChild(i).gameObject);
                        print("destroying extra lobbyitem");
                    }
                }
            }

            if (!bot)
                return;
        }

        localLobbyPlayer = PhotonView.Find(id).GetComponent<RectTransform>();
        Color col = localLobbyPlayer.GetComponentsInChildren<Image>()[1].color;
        col.a = 255;
        localLobbyPlayer.GetComponentsInChildren<Image>()[1].color = col;
    }
    [PunRPC]
    void ReadyBoard() //Setup gameboard for gameplay [DONE]
    {
        lobbyPanel.SetActive(false);
        header.SetActive(true);
        OrderPlayers();
    }
    [PunRPC]
    public void Rematch() //Reload game scene so players can rematch [DONE]
    {
        PhotonNetwork.LeaveRoom();
        SceneManagerLoad.Instance.Load(2);
    }
    [PunRPC]
    public void LeaveMatch(int playerIndex) //Leave the current game  [DONE]
    {
        if(playerIndex != -1)
        {
            if(players[playerIndex].playerName == PhotonNetwork.MasterClient.NickName && players.Count - botCount != 1)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].botPlayer)
                        players.RemoveAt(i);
                }
            }
            players.RemoveAt(playerIndex);
        }

        if (players.Contains(localPlayer) == false)
        {
            PhotonNetwork.LeaveRoom();
            SceneManagerLoad.Instance.Load(1);
        }
        else if (players.Count == 1)
        {
            currentPlayer = localPlayer;
            localPlayer.gameObject.SetActive(false);
            Win();
        }
    }
    [PunRPC]
    void SpawnBot(int number) //Spawn a bot player in the game [DONE]
    {
        GameObject botObj = PhotonNetwork.Instantiate("OpponentPlayerBase", Vector3.zero, Quaternion.identity);
        Player bot = botObj.GetComponentInChildren<Player>();
        int randomSpriteIndex = Random.Range(0, 8);
        string randomName = "Bot_" + PlayerManager.Instance.randomNames[Random.Range(0, PlayerManager.Instance.randomNames.Length)];

        localLobbyPlayer = PhotonNetwork.Instantiate("lobbyPlayer", Vector3.zero, Quaternion.identity).GetComponent<RectTransform>();
        bot.GetComponent<PhotonView>().RPC("InitPlayer", RpcTarget.AllBuffered, randomName, randomSpriteIndex, randomSpriteIndex, true);
        GetComponent<PhotonView>().RPC("SyncLobbyPlayer", RpcTarget.AllBuffered, localLobbyPlayer.GetComponent<PhotonView>().ViewID, randomSpriteIndex, randomName, false, true);
        if (players.Count == PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            PhotonNetwork.CurrentRoom.IsVisible = false;
            NetworkStartGame(startingCardCount);
        }
    }
    [PunRPC]
    void EnableCall()
    {
        int prevIndex = players.IndexOf(currentPlayer) - 1;

        if (prevIndex < 0)
            prevIndex = players.Count - 1;

        if (players[prevIndex] == localPlayer)
            return;
        print("CALL OUT POSSIBLE");
        callOutButton.SetActive(true);
    }
    #endregion

    #region Others
    public void Win() //When a player wins [DONE]
    {
        //disable stuff
        bgColour.gameObject.SetActive(false);
        trashPile.gameObject.SetActive(false);
        continueButton.SetActive(false);
        colourPicker.SetActive(false);
        header.SetActive(false);

        //vinegette zoom in
        StartCoroutine(StartVinegette());
    }
    public void Switch() //Switch the direction of the arrow in the header object [DONE]
    {
        direction.GetComponent<RectTransform>().localEulerAngles = new Vector3(0, direction.GetComponent<RectTransform>().localEulerAngles.y + 180, 0);
    }
    IEnumerator StartVinegette() //Win sequence coroutine [DONE]
    {
        Vignette vignette;
        ChromaticAberration chromAb;
        float t = 0;

        volume.profile.TryGetSettings(out vignette);
        volume.profile.TryGetSettings(out chromAb);

        while (t < winFadeTime) //over time increase the vignette to create a zoom effect
        {
            t += Time.deltaTime;
            vignette.intensity.value = Mathf.Lerp(0,1,t/ winFadeTime); 
            chromAb.intensity.value = Mathf.Lerp(0, 1, t / winFadeTime);
            yield return null;
        }

        unoWin.SetActive(true);
        btnRematch.SetActive(true);
        btnMainMenu.SetActive(true);
        winnerPanel.SetActive(true);
        winnerPic.sprite = currentPlayer.userPic;
        winnerText.text = currentPlayer.playerName + " Wins!";
        btnRematch.GetComponent<Button>().onClick.AddListener(delegate { NetworkRematch(); });
        btnMainMenu.GetComponent<Button>().onClick.AddListener(delegate { NetworkLeaveMatch(); });
    }
    IEnumerator Call(int c) //When a player calls / calls out [DONE]
    {
        callUnoButton.SetActive(false);
        callOutButton.SetActive(false);

        if (c == 0) //call
        {
            called = true;
            callHeader.SetActive(true);
            callHeader.GetComponentInChildren<TextMeshProUGUI>().text = "<#67D126>Called Uno!";
        } 
        else if(!called)//call out for not saying uno
        {
            currentPlayer.playerCover.GetComponent<Image>().enabled = true;
            playCount = 0;
            callHeader.SetActive(true);
            callHeader.GetComponentInChildren<TextMeshProUGUI>().text = "<#E65048>Called Out!";

            if (PhotonNetwork.IsMasterClient) //Called out on
            {
                plusCards = 2;
                cycleCount = players.Count - 1;
                processCount = 0;
                print("Master is waiting for process count to be 1 below players.count IN CALLLLLL");
                while (processCount < (players.Count - botCount) - 1)
                {
                    NetworkSyncProcess(1, "master");
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                yield return new WaitUntil(() => processCount >= (players.Count - botCount) - 1);
                print("Ended CALLL on all clients with processCount of " + processCount);
                NetworkCyclePlayers(); //Only called if master client so they must wait for everyone else to update
            }
            yield break;
        }       
    }
    public void DrawPile(bool sync) //Draw a card from draw pile [DONE]
    {
        NetworkDrawCard(sync);
    }
    public void OrderPlayers() //Order appearance of players in order of who presses "READY" [DONE]
    {
        int myIndex = players.IndexOf(localPlayer);
        Player[] newOrder = new Player[players.Count-1];
        newOrder[0] = players[(myIndex + 1) % players.Count];
        if (players.Count > 2)
            newOrder[1] = players[(myIndex + 2) % players.Count];
        if (players.Count > 3)
            newOrder[2] = players[(myIndex + 3) % players.Count];
        for (int i = 0; i < newOrder.Length; i++)
            newOrder[i].parentHolder.transform.SetSiblingIndex(i);
    }
    #endregion
}
