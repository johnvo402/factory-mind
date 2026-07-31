export interface ServerSentEvent {
  event: string;
  data: string;
}

export async function* parseServerSentEvents(
  stream: ReadableStream<Uint8Array>,
): AsyncGenerator<ServerSentEvent> {
  const reader = stream.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (true) {
      const { done, value } = await reader.read();
      buffer += decoder.decode(value, { stream: !done });

      let boundary = findEventBoundary(buffer);
      while (boundary) {
        const frame = buffer.slice(0, boundary.index);
        buffer = buffer.slice(boundary.index + boundary.length);
        const event = parseFrame(frame);
        if (event) {
          yield event;
        }

        boundary = findEventBoundary(buffer);
      }

      if (done) {
        const event = parseFrame(buffer);
        if (event) {
          yield event;
        }
        return;
      }
    }
  } finally {
    reader.releaseLock();
  }
}

function findEventBoundary(value: string): { index: number; length: number } | null {
  const match = /\r?\n\r?\n/.exec(value);
  return match ? { index: match.index, length: match[0].length } : null;
}

function parseFrame(frame: string): ServerSentEvent | null {
  let event = 'message';
  const data: string[] = [];

  for (const line of frame.split(/\r?\n/)) {
    if (!line || line.startsWith(':')) {
      continue;
    }

    const separator = line.indexOf(':');
    const field = separator < 0 ? line : line.slice(0, separator);
    let value = separator < 0 ? '' : line.slice(separator + 1);
    if (value.startsWith(' ')) {
      value = value.slice(1);
    }

    if (field === 'event') {
      event = value;
    } else if (field === 'data') {
      data.push(value);
    }
  }

  return data.length > 0 ? { event, data: data.join('\n') } : null;
}
