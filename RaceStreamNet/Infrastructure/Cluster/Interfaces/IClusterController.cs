using Akka.Actor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Cluster.Interfaces;

public interface IClusterController
{
    public IActorRef GetShardRegion();

    public Task Stop();
}