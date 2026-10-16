#import <AVFoundation/AVFoundation.h>
extern "C" void SQ_AudioSessionPlayback(void) {
    AVAudioSession *s = [AVAudioSession sharedInstance];
    [s setCategory:AVAudioSessionCategoryPlayback error:nil];
    [s setActive:YES error:nil];
}
