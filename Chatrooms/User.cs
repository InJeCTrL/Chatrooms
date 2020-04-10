namespace Chatrooms
{
    public class User
    {
        public string ID { get; set; }
        public string NickName { get; set; }
        public string RoomID { get; set; }
        public User(string ConnectionID, string NickName)
        {
            ID = ConnectionID;
            this.NickName = NickName;
            RoomID = string.Empty;
        }
        public void JoinRoom(Room room)
        {
            RoomID = room.ID;
        }
        public void QuitRoom()
        {
            RoomID = string.Empty;
        }
    }
}
