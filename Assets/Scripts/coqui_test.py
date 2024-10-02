

import json
from threading import Thread
from simple_websocket_server import WebSocketServer, WebSocket
import uvicorn
import io
import wave




class SimpleEcho(WebSocket):

    def handle(self):
        # echo message back to client
        #self.send_message(self.data)
        stream.feed(str(self.data))
        soundgen.startStreamIdempotently()
        

    def connected(self):
        print("Connected, ", self)
        clients.append(self)
        print("Appended")


    def handle_close(self):
        print(self.address, 'closed')
        clients.remove(self)
    
    def killMyself(self):
        self.close()
    
    def sendNotification(self):
        self.send_message(str("answer"))
        
clients = []
class ttsGenerator():
    def __init__(self) -> None:
        self.chunkCounter = 0
        self.chunkLimit = 350
        self.wavFrames = io.BytesIO()
        
        
    def getWavFile(self):
        return self.wavFile

    def startStreamIdempotently(self):
        self.startStream()

    def startStream(self):
        self.isPlaying = True
        playThread = Thread(target=self.playWrapper())
        playThread.start()


    def playWrapper(self):
        try:
            stream.play_async(on_audio_chunk=self.onChunk, on_sentence_synthesized=self.onSentence, muted=True)
        except:
            print("An exception occurred") 
        
    def onChunk(self, chunki):
        self.wavFrames.write(chunki)
        self.chunkCounter = self.chunkCounter + 1
        #if self.chunkCounter >= self.chunkLimit:
        #    print("Chunk limit reached, friendly asking for finalize")
        #    self.finalizeWav(self.wavFrames)
    
    def onSentence(self, chunk):
        self.finalizeWav(self.wavFrames)
    
    def create_wave_with_engine(self, engine, chunks):
        format, channels, sample_rate = engine.get_stream_info()
        print("format: ", format, "channels ", channels, "sample_rate: ", sample_rate)

        
        '''
        Coqui: 24000 sample rate
        System: 22050 sample rate
        '''
        num_channels = 1  
        sample_width = 2   
        frame_rate = sample_rate
        chunks.seek(0)
        chunkbyte = chunks.read()
        wav_header = io.BytesIO()
        with wave.open(wav_header, 'wb') as wav_file:
            wav_file.setnchannels(num_channels)
            wav_file.setsampwidth(sample_width)
            wav_file.setframerate(frame_rate)
            wav_file.writeframes(chunkbyte)
            
        print("Wav_header after writing: ", wav_header.getbuffer().nbytes)
        wav_header.seek(0)
        return wav_header

        #wav_header.seek(0)
        #wave_header_bytes = wav_header.read()
        #wav_header.close()

        # Create a new BytesIO with the correct MIME type for Firefox
        #final_wave_header = io.BytesIO()
        #final_wave_header.write(wave_header_bytes)
        #final_wave_header.seek(0)
    
        #return wav_header #.getvalue()
    
    def finalizeWav(self, wavFile:io.BytesIO):
        print("Finalizing, testing for length")
        fileLen = self.wavFrames.getbuffer().nbytes
        print("File length: ", fileLen)
        if fileLen < 1024:
            #File is most likely empty sound and too small to be picked up by Unity Multimedia request. 
            self.wavFrames = io.BytesIO()
            self.chunkCounter = 0
            return
        wavobjectlist.append(self.create_wave_with_engine(engine, self.wavFrames).getvalue())
        self.wavFrames = io.BytesIO()
        self.chunkCounter = 0
        sendAck()
        
    
    def onStreamStopInternal(self):
        print("finalizing")
        self.finalizeWav(self.wavFrames)    


def runWebsocketServer():
    websocketServer.serve_forever()
        
def exit_handler():
    print("We're ending.")
    engine.shutdown()
    websocketServer.close()
    
def onStreamStop():
    print("Asking soundgen to finalize")
    soundgen.onStreamStopInternal()
    
def sendAck():
    websocketServer.websocketclass.sendNotification(clients[0])
    



if __name__ == '__main__':
    from RealtimeTTS import TextToAudioStream, CoquiEngine, SystemEngine   
    from fastapi import FastAPI
    from fastapi.responses import StreamingResponse 
    import uuid
    

    print("Firing up Engine")
    global engine
    #engine = CoquiEngine()
    engine = SystemEngine()
    
    
    print("Creating EngineStream")
    global stream
    stream = TextToAudioStream(engine, on_audio_stream_stop=onStreamStop)
    
    global websocketServer
    websocketServer = WebSocketServer("", 8000, SimpleEcho)
    
    global soundgen
    soundgen = ttsGenerator()

    print("Running WebsocketServer")
    socketThread = Thread(target=runWebsocketServer)
    socketThread.start()
    
    global wavobjectlist
    wavobjectlist = []
    
    
    
    app = FastAPI()
    
    def iterfile():  # 
        with open("testwav.wav", mode="rb") as file_like:  # 
            yield from file_like
    
    @app.get("/")
    async def read_root():
       
        waveElement = io.BytesIO(wavobjectlist.pop(0))               # Receive next small, finished .wav file from list #
                                                          # Update Soundfilecount                            #
        print("Got a download request")
        return StreamingResponse(                                    # Return it as AudioStream and generate random ID as name. 
            content=waveElement,
            status_code=200,
            media_type="audio/wav",
            headers={"Access-Control-Expose-Headers": "Content-Disposition", "Content-Disposition": "attachment; filename=" + str(uuid.uuid4()) + ".wav",'Access-Control-Expose-Headers': 'Content-Disposition'},
        )
    
    @app.get("/kolja")
    async def streamGenerator():
        return StreamingResponse(
            ttsGenerator.audio_chunk_generator(browser_request),
            media_type="audio/wav"
        )
    
    uvicorn.run(app, host="0.0.0.0", port=8001)
    exit_handler()