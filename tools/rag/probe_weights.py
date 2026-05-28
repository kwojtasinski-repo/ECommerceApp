"""Quick probe to verify .NET ranker applies weights from rag-config.yaml."""
import json, urllib.request, sys

def probe(port, label, question, top_k=10):
    base = f'http://localhost:{port}'
    hdr = {'Content-Type': 'application/json', 'Accept': 'application/json, text/event-stream'}
    def post(body, extra=None):
        req = urllib.request.Request(base, data=json.dumps(body).encode(),
                                     method='POST', headers={**hdr, **(extra or {})})
        return urllib.request.urlopen(req, timeout=60)
    r = post({'jsonrpc':'2.0','id':1,'method':'initialize',
              'params':{'protocolVersion':'2024-11-05','capabilities':{},
                        'clientInfo':{'name':'probe','version':'1'}}})
    sid = r.headers.get('mcp-session-id', ''); r.read()
    extra = {'mcp-session-id': sid} if sid else {}
    post({'jsonrpc':'2.0','method':'notifications/initialized','params':{}}, extra).read()
    r = post({'jsonrpc':'2.0','id':2,'method':'tools/call',
              'params':{'name':'query_docs','arguments':{'question':question,'top_k':top_k}}}, extra)
    text = r.read().decode()
    for line in text.splitlines():
        if line.startswith('data:'):
            body = json.loads(line[5:].strip())
            raw = body.get('result',{}).get('content',[{}])[0].get('text','{}')
            d = json.loads(raw)
            print(f'\n=== {label} :{port} === {question}')
            for i,h in enumerate(d.get('hits',[])[:top_k],1):
                src = h.get('source') or h.get('rel_path') or '?'
                print(f'  {i}. {h.get("score",0):.3f}  {src}')
            return d.get('hits',[])
    return []

if __name__ == '__main__':
    q = sys.argv[1] if len(sys.argv) > 1 else 'FluentAssertions AwesomeAssertions known issue'
    probe(3002, 'python', q)
    probe(3001, 'dotnet', q)
