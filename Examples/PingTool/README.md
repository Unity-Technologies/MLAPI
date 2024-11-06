# Netcode for GameObjects Ping Tool
This project is only for example purposes and provides one example of how to one can acquire ping times between clients during a network session. It also includes integration with the Realtime Network Stats Monitor tool that provides additional relative (and useful) network stats.

## Ping Tool
The ping tool lives in its own assembly for convenience purposes:

![image](https://github.com/user-attachments/assets/d2fdecda-e8ec-4d1a-ae6e-4590c6fcce40)

The **Sample** scene includes an in-scene placed instance of the **PingTool**:

![image](https://github.com/user-attachments/assets/4e7c8af1-bc15-4b93-95f1-86367abea5ab)

The **Net Stats Monitor Toggle** property allows you to select the key to press to toggle the RNSM visibility (the **tab** key is the default).

The ping tool includes a runtime "Ping Rate" slider that provides you with the ability to adjust the number of pings to send per second. This is only to demonstrate you really don't need to send more than 1 or 2 pings per second to get reasonable results.


## Ping Tool RNSM Integration
Depending upon the network topology selected in the **NetworkManager** depends upon how information is displayed.
The RTT values represnt the UnitTransport's Round Trip Time calculated values.
The Ping values represent the time it takes to send a message from client-a to client-b which includes the time it takes to be serialized, added to the outbound queue, sent via UTP, received by UTP, deserialized and processed on the client-b side. The delta time is based off of the delta network server time on client-a and client-b.

### Client-Server Network Topology
When using a client-server network topology, upon first starting a host with no other connected clients you should see no stats other than the frame frate:
**(Host View)**

![image](https://github.com/user-attachments/assets/9c5c16f7-bb41-4c6b-945d-b3dfcb3a3fea)

When you connect one or more clients:
- From the host side, you will begin to see the RTT and ping times to each connected client:
**(Host View)**
  
![image](https://github.com/user-attachments/assets/b130f453-6acf-4e1e-b264-d69c7ae7be23)

- From a client side, you will see the RTT to the server and a ping time for each connected client:
**(Client-2 View)**
  
![image](https://github.com/user-attachments/assets/73e7d93a-5f6c-4c0f-b044-ef35315f33a0)

### Distributed Authority Network Topology
When using a distributed authority network topology, upon creating a session you will immediately see the RTT to the service displayed:

![image](https://github.com/user-attachments/assets/c1f36a49-9986-4b26-b78d-feadc8f00d2a)

As more clients join the session you will start to see the ping times to each client:

![image](https://github.com/user-attachments/assets/06abcaac-693f-4b7f-bf13-03c63dd684bf)

## About This Example
While we will make efforts to update this project, it is for example purposes only and could have bugs or even areas where it could be improved. If you plan on using this example or the PingTool assembly for your project, we recommend you cusotmize it depending upon your project's needs and goals.







