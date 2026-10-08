"""Signal-safety tests; artistic acceptance still requires human listening."""
import tempfile
import unittest
from pathlib import Path

import numpy as np

import build_discovery as cue


class DiscoveryCueTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.audio = cue.build()

    def test_rebuild_is_deterministic(self):
        np.testing.assert_array_equal(self.audio, cue.build())

    def test_intersample_headroom_and_silent_edges(self):
        self.assertTrue(np.isfinite(self.audio).all())
        self.assertLess(cue.true_peak(self.audio), .84)
        self.assertLess(np.max(abs(self.audio.mean(axis=0))), .001)
        self.assertTrue((self.audio[[0, -1]] == 0).all())
        self.assertLess(np.max(abs(self.audio[-480:])), .001)

    def test_contact_is_prompt_and_tail_does_not_mask_gameplay(self):
        self.assertGreater(np.max(abs(self.audio[:1920])), .02)
        onset = np.sqrt(np.mean(self.audio[:19200] ** 2))
        tail = np.sqrt(np.mean(self.audio[-9600:] ** 2))
        self.assertGreater(onset, .15)
        self.assertLess(tail, .008)

    def test_mono_fold_preserves_energy(self):
        stereo = np.mean(self.audio ** 2)
        mono = np.mean(self.audio.mean(axis=1) ** 2)
        self.assertGreater(mono / stereo, .9)

    def test_delivered_pcm_format_and_quantization(self):
        with tempfile.TemporaryDirectory(prefix="astra-audio-test-") as directory:
            path = Path(directory) / "cue.wav"
            cue.write_pcm(path, self.audio)
            read, rate = cue.read_pcm(path)
            self.assertEqual(rate, 48000)
            self.assertEqual(read.shape, (93600, 2))
            self.assertLess(np.max(abs(read - self.audio)), .00005)


if __name__ == "__main__":
    unittest.main()
