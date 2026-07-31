import { parseServerSentEvents, ServerSentEvent } from './server-sent-events';

describe('parseServerSentEvents', () => {
  it('parses events split across chunks and supports CRLF frames', async () => {
    const stream = streamFrom([
      'event: conver',
      'sation\r\ndata: {"conversationId":"conversation-1"}\r\n\r\n',
      'event: token\ndata: {"content":"Hello"}\n\n',
      'event: citations\ndata: {"citations":[]}\n\nevent: done\ndata: {}\n\n',
    ]);

    const events: ServerSentEvent[] = [];
    for await (const event of parseServerSentEvents(stream)) {
      events.push(event);
    }

    expect(events).toEqual([
      { event: 'conversation', data: '{"conversationId":"conversation-1"}' },
      { event: 'token', data: '{"content":"Hello"}' },
      { event: 'citations', data: '{"citations":[]}' },
      { event: 'done', data: '{}' },
    ]);
  });

  it('joins multiple data lines according to the SSE contract', async () => {
    const events: ServerSentEvent[] = [];
    for await (const event of parseServerSentEvents(streamFrom([
      ': keep-alive\nevent: message\ndata: first\ndata: second\n\n',
    ]))) {
      events.push(event);
    }

    expect(events).toEqual([{ event: 'message', data: 'first\nsecond' }]);
  });

  function streamFrom(chunks: string[]): ReadableStream<Uint8Array> {
    const encoder = new TextEncoder();
    return new ReadableStream({
      start(controller) {
        for (const chunk of chunks) {
          controller.enqueue(encoder.encode(chunk));
        }
        controller.close();
      },
    });
  }
});
