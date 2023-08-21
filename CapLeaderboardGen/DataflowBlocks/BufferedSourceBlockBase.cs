using CapLeaderboardGen.DataTypes;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks.Dataflow;

namespace CapLeaderboardGen.DataflowBlocks
{
    internal class BufferedSourceBlockBase<TItem>: IReceivableSourceBlock<TItem>
    {
        private readonly BufferBlock<TItem> sourceBuffer;


        public BufferedSourceBlockBase(DataflowBlockOptions blockOptions)
        {
            sourceBuffer= new BufferBlock<TItem>(blockOptions);
        }

        public BufferedSourceBlockBase()
        {
            sourceBuffer = new BufferBlock<TItem>();
        }

        public Task Completion => sourceBuffer.Completion;

        public virtual void Complete()
        {
            sourceBuffer.Complete();
        }

        public virtual TItem? ConsumeMessage(DataflowMessageHeader messageHeader, ITargetBlock<TItem> target, out bool messageConsumed)
        {
            return ((IReceivableSourceBlock<TItem>)sourceBuffer).ConsumeMessage(messageHeader, target, out messageConsumed);
        }

        public virtual void Fault(Exception exception)
        {
            ((IReceivableSourceBlock<TItem>)sourceBuffer).Fault(exception);
        }

        public virtual IDisposable LinkTo(ITargetBlock<TItem> target, DataflowLinkOptions linkOptions)
        {
            return sourceBuffer.LinkTo(target, linkOptions);
        }

        public virtual void ReleaseReservation(DataflowMessageHeader messageHeader, ITargetBlock<TItem> target)
        {
            ((IReceivableSourceBlock<TItem>)sourceBuffer).ReleaseReservation(messageHeader, target);
        }

        public virtual bool ReserveMessage(DataflowMessageHeader messageHeader, ITargetBlock<TItem> target)
        {
            return ((IReceivableSourceBlock<TItem>)sourceBuffer).ReserveMessage(messageHeader, target);
        }

        public virtual bool TryReceive(Predicate<TItem>? filter, [MaybeNullWhen(false)] out TItem item)
        {
            return sourceBuffer.TryReceive(filter, out item);
        }

        public virtual bool TryReceiveAll([NotNullWhen(true)] out IList<TItem>? items)
        {
            return sourceBuffer.TryReceiveAll(out items);
        }

        protected bool PostToBuffer(TItem item)
        {
            return sourceBuffer.Post<TItem>(item);
        }
    }
}