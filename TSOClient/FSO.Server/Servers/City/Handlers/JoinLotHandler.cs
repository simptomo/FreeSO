﻿using FSO.Server.Common;
using FSO.Server.Database.DA;
using FSO.Server.Database.DA.Shards;
using FSO.Server.Framework.Gluon;
using FSO.Server.Framework.Voltron;
using FSO.Server.Protocol.Electron.Packets;
using FSO.Server.Protocol.Gluon.Packets;
using FSO.Server.Servers.City.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSO.Server.Servers.City.Handlers
{
    public class JoinLotHandler
    {
        private LotServerPicker PickingEngine;
        private LotAllocations Lots;
        private IDAFactory DAFactory;

        public JoinLotHandler(LotAllocations lots, LotServerPicker pickingEngine, IDAFactory da)
        {
            this.Lots = lots;
            this.PickingEngine = pickingEngine;
            this.DAFactory = da;
        }

        public async void Handle(IVoltronSession session, FindLotRequest packet)
        {
            var find = await Lots.TryFindOrOpen(packet.LotId, session);
            
            if(find.Status == Protocol.Electron.Model.FindLotResponseStatus.FOUND){

                ShardTicket ticket = null;

                using (var db = DAFactory.Get())
                {
                    //I need a shard ticket so I can connect to the lot server and assume the correct avatar
                    ticket = new ShardTicket
                    {
                        ticket_id = Guid.NewGuid().ToString().Replace("-", ""),
                        user_id = session.UserId,
                        avatar_id = session.AvatarId,
                        date = Epoch.Now,
                        ip = session.IpAddress
                    };

                    db.Shards.CreateTicket(ticket);
                }

                session.Write(new FindLotResponse {
                    Status = find.Status,
                    LotId = packet.LotId,
                    LotServerTicket = ticket.ticket_id,
                    Address = find.Server.PublicHost,
                    User = session.UserId.ToString()
                });
            }
            else
            {
                session.Write(new FindLotResponse {
                    Status = find.Status,
                    LotId = packet.LotId
                });
            }
        }

        public void Handle(IGluonSession session, TransferClaimResponse claimResponse)
        {
            if(claimResponse.Type == Protocol.Gluon.Model.ClaimType.LOT)
            {
                Lots.OnTransferClaimResponse(claimResponse);
            }
        }
    }
}