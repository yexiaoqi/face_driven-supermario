using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json.Linq;
using System;
using System.IO;


// connect with ZeroMQ
public class NetMqListener
{
    public string sub_to_ip;
    public string sub_to_port;
    public bool facsvatar_logging = false;
    private readonly Thread _listenerWorker;
    private bool _listenerCancelled;
    public delegate void MessageDelegate(List<string> msg_list);
    private readonly MessageDelegate _messageDelegate;
    private readonly ConcurrentQueue<List<string>> _messageQueue = new ConcurrentQueue<List<string>>();
    //private string csv_folder = "Assets/Logging/";
    private string csv_path = "Assets/Logging/unity_timestamps_sub.csv";
    private StreamWriter csv_writer;
    private long msg_count;
    public NetMqListener(string sub_to_ip, string sub_to_port)
    {
        this.sub_to_ip = sub_to_ip;
        this.sub_to_port = sub_to_port;
    }

    private void ListenerWork()
    {
        Debug.Log("Setting up subscriber sock");
        AsyncIO.ForceDotNet.Force();
        using (var subSocket = new SubscriberSocket())
        {
            // set limit on how many messages in memory
            subSocket.Options.ReceiveHighWatermark = 1000;
            // socket connection
            // subSocket.Connect("tcp://localhost:5572");
            subSocket.Connect("tcp://" + sub_to_ip + ":" + sub_to_port);
            // subscribe to topics; "" == all topics
            subSocket.Subscribe("");
            Debug.Log("sub socket initiliased");

            string topic;
            //string frame;
            string timestamp;
            //string blend_shapes;
            //string head_pose;
            string facsvatar_json;
            while (!_listenerCancelled)
            {
                //string frameString;
                // wait for full message
                //if (!subSocket.TryReceiveFrameString(out frameString)) continue;
                //Debug.Log(frameString);
                //_messageQueue.Enqueue(frameString);

                List<string> msg_list = new List<string>();
                if (!subSocket.TryReceiveFrameString(out topic)) continue;
                //if (!subSocket.TryReceiveFrameString(out frame)) continue;
                if (!subSocket.TryReceiveFrameString(out timestamp)) continue;
                //if (!subSocket.TryReceiveFrameString(out blend_shapes)) continue;
                //if (!subSocket.TryReceiveFrameString(out head_pose)) continue;
                if (!subSocket.TryReceiveFrameString(out facsvatar_json)) continue;

                //Debug.Log("Received messages:");
                //Debug.Log(frame);
                //Debug.Log(timestamp);
                //Debug.Log(facsvatar_json);

                // check if we're not done; timestamp is empty
                if (timestamp != "")
                {
                    msg_list.Add(topic);
                    msg_list.Add(timestamp);
                    msg_list.Add(facsvatar_json);
                    long timeNowMs = UnixTimeNowMillisec();
                    msg_list.Add(timeNowMs.ToString());  // time msg received; for unity performance

                    if (facsvatar_logging == true)
                    {
                        //Debug.Log("NetMqListener log");

                        //Debug.Log(timeNowMs);
                        //Debug.Log(timestamp2);
                        //Debug.Log(timeNowMs - timestamp2);

                        // write to csv
                        // string csvLine = string.Format("{0},{1},{2}", msg_count, timestamp2, timeNowMs);
                        string csvLine = string.Format("{0},{1}", msg_count, timeNowMs);
                        csv_writer.WriteLine(csvLine);
                    }
                    msg_count++;

                    _messageQueue.Enqueue(msg_list);
                }
                // done
                else
                {
                    Debug.Log("Received all messages");
                }
            }
            subSocket.Close();
        }
        NetMQConfig.Cleanup();
    }

    public static long UnixTimeNowMillisec()
    {
        DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        long unixTimeStampInTicks = (DateTime.UtcNow - unixStart).Ticks;
        long timeNowMs = unixTimeStampInTicks / (TimeSpan.TicksPerMillisecond / 10000);  // 100ns
        //Debug.Log(timeNowMs);
        return timeNowMs;
    }

    // check queue for messages
    public void Update()
    {
        while (!_messageQueue.IsEmpty)
        {
            List<string> msg_list;
            if (_messageQueue.TryDequeue(out msg_list))
            {
                _messageDelegate(msg_list);
            }
            else
            {
                break;
            }
        }
    }

    // threaded message listener
    public NetMqListener(MessageDelegate messageDelegate)
    {
        _messageDelegate = messageDelegate;
        _listenerWorker = new Thread(ListenerWork);
    }

    public void Start()
    {
        if (facsvatar_logging == true)
        {
            // logging
            Debug.Log("Setting up Logging NetMqListener");
            msg_count = -1;
            File.Delete(csv_path);  // delete previous csv if exist
            csv_writer = new StreamWriter(csv_path, true);  // , true
            csv_writer.WriteLine("msg,time_prev,time_now");
            csv_writer.Flush();
            //csv_writer.Close();
            //csv_writer.Open();
            //csv_writer.WriteLine("time_prev,time_now");
            //csv_writer.Close();
        }

        _listenerCancelled = false;
        _listenerWorker.Start();
    }

    public void Stop()
    {
        _listenerCancelled = true;
        _listenerWorker.Join();
        if (facsvatar_logging == true)
        {
            csv_writer.Close();
        }
    }
}



public class MarioCtrl : MonoBehaviour {


    private NetMqListener _netMqListener;
    public string sub_to_ip = "127.0.0.1";
    public string sub_to_port = "5572";
    public bool facsvatar_logging = false;


    // logging
    private long msg_count;
    private string csv_folder = "Assets/Logging/";
    private string csv_path = "Assets/Logging/unity_timestamps_sub.csv";
    private StreamWriter csv_writer;
    private string csv_path_total = "Assets/Logging/unity_timestamps_total.csv";
    private StreamWriter csv_writer_total;


    public float runSpeed = 1f;//跑 -> 速度
    public float movePower = 10f;
    public Animator myAnimator;
    public float jumpSeed = 2f;
    public bool isGround = false;
    public bool isJump = false;
    public bool isRun = false;
    public Transform[] groundCheck;
    private Transform myTransform;
    public Transform headCheck;

    private MPJoystick moveJoystick;
    private GameObject JoyHandle;

    private Transform joyJump;
    private Rect jumpRect;

    void Awake()
    {
        myAnimator = GetComponent<Animator>();
        myTransform = transform;
        JoyHandle = GameObject.FindGameObjectWithTag("JoyHandle");
        moveJoystick = JoyHandle.transform.Find("Joy").GetComponent<MPJoystick>();
        joyJump = JoyHandle.transform.Find("jump");
        
       
    }

	// Use this for initialization
	void Start ()
    {

        if (facsvatar_logging == true)
        {
            // logging
            Debug.Log("Setting up Logging ZeroMQFACSvatar");
            msg_count = -1;

            Directory.CreateDirectory(csv_folder);
            File.Delete(csv_path);  // delete previous csv if exist
            csv_writer = new StreamWriter(csv_path, true);  // true; keep steam open
            csv_writer.WriteLine("msg,time_prev,time_now");
            csv_writer.Flush();

            File.Delete(csv_path_total);  // delete previous csv if exist
            csv_writer_total = new StreamWriter(csv_path_total, true);  // true; keep steam open
            csv_writer_total.WriteLine("msg,time_prev,time_now");
            csv_writer_total.Flush();
        }

        _netMqListener = new NetMqListener(HandleMessage);
        _netMqListener.sub_to_ip = sub_to_ip;
        _netMqListener.sub_to_port = sub_to_port;
        _netMqListener.Start();


        Vector3 pos = Camera.main.ViewportToScreenPoint(joyJump.position);
        //Debug.Log(pos);
        Rect guiRect = GetNewMethod().pixelInset;
        Debug.Log("guiRect" + guiRect);
        jumpRect = new Rect(pos.x + guiRect.x, (pos.y) + guiRect.y, guiRect.width, guiRect.height);

        headCheck = transform.Find("HeadCheck");
        if (groundCheck == null || groundCheck.Length < 2)
        {
            groundCheck = new Transform[2];
            groundCheck[0] = transform.GetChild(0);
            groundCheck[1] = transform.GetChild(1);
        }
    }

    private GUITexture GetNewMethod()
    {
        return NewMethod1();
    }

    private GUITexture NewMethod1()
    {
        return joyJump.gameObject.GetComponent<GUITexture>();
    }



    private void HandleMessage(List<string> msg_list)
    {
        JObject facsvatar = JObject.Parse(msg_list[2]);
        // get Blend Shape dict
        JObject blend_shapes = facsvatar["blendshapes"].ToObject<JObject>();
        // get head pose data
        JObject head_pose = facsvatar["pose"].ToObject<JObject>();

        // split topic to determine target human model
        //Debug.Log(msg_list[0]);
        string[] topic_info = msg_list[0].Split('.'); // "facsvatar.S01_P1.p0.dnn" ["facsvatar", "S01_P1", "p0", "dnn"]
        if (facsvatar_logging == true)
        {
            // logging
            //Debug.Log("ZeroMQFACSvatar log");
            long timeNowMs = UnixTimeNowMillisec();
            long timestampMsgArrived = Convert.ToInt64(msg_list[3]);
            //Debug.Log(timeNowMs);
            //Debug.Log(timestampMsgArrived);
            //Debug.Log(timeNowMs - timestampMsgArrived);

            // write to csv
            string csvLine = string.Format("{0},{1},{2}", msg_count, timestampMsgArrived, timeNowMs);
            csv_writer.WriteLine(csvLine);

            // if data contains timestamp_utc, write total time      
            if (facsvatar["timestamp_utc"] != null)
            {
                //Debug.Log(facsvatar["timestamp_utc"]);
                long timeFirstSend = Convert.ToInt64(facsvatar["timestamp_utc"].ToString());
                //Debug.Log((timeNowMs - timeFirstSend) / 10000);

                // write to csv
                string csvLine_total = string.Format("{0},{1},{2}", msg_count, timeFirstSend, timeNowMs);
                csv_writer_total.WriteLine(csvLine_total);
            }
        }

        msg_count++;
        isGround = Physics2D.Linecast(myTransform.position, groundCheck[0].position, 1 << LayerMask.NameToLayer("Ground"))
                   || Physics2D.Linecast(myTransform.position, groundCheck[1].position, 1 << LayerMask.NameToLayer("Ground"));


        isRun = false;

        float touchKey_x = 1;//moveJoystick.position.x;

        if (Input.GetKey(KeyCode.A))
        //if (touchKey_x < -0.1f)
        {
            transform.localEulerAngles = new Vector3(0, 180, 0);
            if (Camera.main.WorldToScreenPoint(transform.position).x > 20)  //小玛丽不能超出坐屏幕
            {
                transform.Translate(Vector3.right * runSpeed * Time.deltaTime * Mathf.Abs(touchKey_x)); //移动位置
            }
            runAnim();
        }

        //if(float(head_pose.Last)>0)
        if (Input.GetKey(KeyCode.D))
        //else if (touchKey_x > 0.1f)
        {
            transform.localEulerAngles = new Vector3(0, 0, 0);
            transform.Translate(Vector3.right * runSpeed * Time.deltaTime * Mathf.Abs(touchKey_x));
            runAnim();
        }

        if (Input.GetKeyDown(KeyCode.W) && isGround)
        //if (isTouchJump()&&isGround)
        {
            World.playAudio(World.jumpAudioIndex);
            //<<<<<<< HEAD
            //
            //GetComponent<Rigidbody2D>().velocity = new Vector2(0, jumpSeed);
            //=======
            GetComponent<Rigidbody2D>().velocity = new Vector2(0, jumpSeed);
            //>>>>>>> 8c9a1df50fb98a4abbda29382c341737c5fca2fe
            jumpAnim();
        }

        if (!isRun && isGround)
        {
            if (!isJump)
            {
                standAnim();
            }
            //<<<<<<< HEAD
            //else if (GetComponent<Rigidbody2D>().velocity.y <= 0)
            //=======
            else if (GetComponent<Rigidbody2D>().velocity.y <= 0)
            //>>>>>>> 8c9a1df50fb98a4abbda29382c341737c5fca2fe
            {
                isJump = false;
            }
        }



        /* if(Physics2D.Linecast(myTransform.position, headCheck.position, 1 << LayerMask.NameToLayer("Weak")))
         {
             Debug.Log("I can play!");
         }*/
    }

    public static long UnixTimeNowMillisec()
    {
        DateTime unixStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc);
        long unixTimeStampInTicks = (DateTime.UtcNow - unixStart).Ticks;
        long timeNowMs = unixTimeStampInTicks / (TimeSpan.TicksPerMillisecond / 10000);  // 100ns
        //Debug.Log(timeNowMs);
        return timeNowMs;
    }


    // Update is called once per frame
    void Update () {

        _netMqListener.Update();

  
	}

    void LateUpdate()
    {
        transform.localEulerAngles = new Vector3(0, myTransform.localEulerAngles.y, 0);
    }


    private void runAnim()
    {
        myAnimator.SetBool("isRun", true);
        myAnimator.SetBool("isJump", false);
        myAnimator.SetBool("isStand", false);
        isRun = true;
        isJump = false;
    }


    private bool isTouchJump()
    {
        for (int i = 0; i < Input.touchCount; i++)
        //if (Input.GetMouseButtonDown(0))
        {
            Debug.Log(Input.mousePosition);
            Debug.Log(jumpRect);
            if (jumpRect.Contains(Input.GetTouch(i).position))
           // if (jumpRect.Contains(Input.mousePosition))
            {
                Debug.Log(true);
                return true;
            }
            Debug.Log(false);
        }
        return false;
    }

    void OnGUI()
    {
       // GUI.Box(jumpRect,"测试框");
    }

    private void jumpAnim()
    {
        myAnimator.SetBool("isRun", false);
        myAnimator.SetBool("isJump", true);
        myAnimator.SetBool("isStand", false);
        isJump = true;
        isRun = false;
    }

    private void standAnim()
    {
        myAnimator.SetBool("isRun", false);
        myAnimator.SetBool("isJump", false);
        myAnimator.SetBool("isStand", true);
        isRun = false;
        isJump = false;
    }


    void OnCollisionEnter2D(Collision2D other)
    {

        if (other.gameObject.tag == "mushroom")
        {
           /* if (Physics2D.Linecast(myTransform.position, groundCheck[0].position, 1 << LayerMask.NameToLayer("Mushroom"))||
                Physics2D.Linecast(myTransform.position, groundCheck[1].position, 1 << LayerMask.NameToLayer("Mushroom")))
            {
                other.collider.isTrigger = true;
                other.gameObject.SendMessage("die");
            }else*/
            {
                GetComponent<ChenkMarioDie>().setMarioDie();
            }
        }
        if ((other.gameObject.tag == "why") && (Physics2D.Linecast(myTransform.position, headCheck.position, 1 << LayerMask.NameToLayer("Weak"))))
        {
            other.gameObject.GetComponent<Mushroomhy>().setHarm();
        }
       
        // Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.tag == "coin")
        {
            World.playAudio(World.coinAudio);
            Destroy(other.gameObject);
        }
        else if (other.tag ==  "flower")
        {
            GetComponent<ChenkMarioDie>().setMarioDie();
        }

        if (other.tag == "mushroom")
        {
            if (other.GetComponent<MushroomCtrl>().isDie)
                return;
            // other.collider.isTrigger = true;
            // other.gameObject.rre
            Destroy(other.GetComponent<BoxCollider2D>());
            if (other != null) { other.gameObject.SendMessage("die"); }
            //other.collider.isTrigger = true;
        }
    }

    private void OnDestroy()
    {
        _netMqListener.Stop();
    }
}
