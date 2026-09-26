import json, urllib.request, hmac, hashlib, base64, struct, time, os, sys
B="http://localhost:5080/api"
STATE=os.path.join(os.path.dirname(__file__),"qa_state.json")
st=json.load(open(STATE)) if os.path.exists(STATE) else {}
def save(): json.dump(st,open(STATE,"w"),indent=1)
def totp(secret):
    k=base64.b32decode(secret.replace(" ","").upper()+"="*((8-len(secret.replace(" ",""))%8)%8))
    c=int(time.time())//30
    h=hmac.new(k,struct.pack(">Q",c),hashlib.sha1).digest(); o=h[-1]&15
    return "%06d"%((struct.unpack(">I",h[o:o+4])[0]&0x7fffffff)%1000000)
def req(method,path,body=None,token=None,critical=False):
    h={"Content-Type":"application/json"}
    tok=token or st.get("token")
    if tok: h["Authorization"]="Bearer "+tok
    if critical: h["X-TOTP-Code"]=totp(st["secret"])
    r=urllib.request.Request(B+path,data=None if body is None else json.dumps(body).encode(),headers=h,method=method)
    try:
        with urllib.request.urlopen(r) as resp:
            t=resp.read().decode(); return resp.status,(json.loads(t) if t else None)
    except urllib.error.HTTPError as e:
        t=e.read().decode()
        try: return e.code,json.loads(t)
        except: return e.code,t
results=[]
def check(name,cond,detail=""):
    results.append((name,bool(cond))); print(("PASS " if cond else "FAIL ")+name+("" if cond else "  -> "+str(detail)[:300]))
