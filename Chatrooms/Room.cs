using System;

namespace Chatrooms
{
    public class Room
    {
        /// <summary>
        /// 房间ID
        /// </summary>
        public string ID { get; set; }
        /// <summary>
        /// 房间标题
        /// </summary>
        public string RoomTitle { get; set; }
        /// <summary>
        /// 房间密码
        /// </summary>
        public string RoomPwd { get; set; }
        /// <summary>
        /// 房间用户数
        /// </summary>
        public long UserCount { get; set; }
        public Room(string Title, string Pwd, User user)
        {
            RoomTitle = Title;
            RoomPwd = Pwd;
            ID = Guid.NewGuid().ToString();
            UserCount = 1;
            user.RoomID = ID;
        }
        public void AddUser()
        {
            ++UserCount;
        }
        public void DecUser()
        {
            --UserCount;
        }
    }
}
